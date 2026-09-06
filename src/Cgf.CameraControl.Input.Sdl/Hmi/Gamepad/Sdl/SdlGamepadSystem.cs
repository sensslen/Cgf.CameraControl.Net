using System.Collections.Concurrent;
using System.Reactive.Subjects;
using Cgf.CameraControl.Core.Logger;
using SDL3;

namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Sdl;

/// Owns SDL and the thread it is pumped on. Every SDL call in the application happens here, on that
/// one thread, because the event queue and the device handles are not safe to touch from anywhere
/// else. Work arriving from the UI or from an interface is queued rather than executed in place.
public sealed class SdlGamepadSystem : IAsyncDisposable
{
    private const int PollTimeoutMs = 5;

    private readonly ILogger _logger;
    private readonly ConcurrentQueue<Action> _work = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly BehaviorSubject<IReadOnlyList<SdlGamepadPresence>> _presence = new([]);
    private readonly List<SdlGamepadDevice> _devices = [];
    private readonly Dictionary<uint, OpenPad> _pads = [];
    private readonly Thread _thread;

    public SdlGamepadSystem(ILogger logger)
    {
        _logger = logger;
        _thread = new Thread(Run) { IsBackground = true, Name = "SDL gamepad" };
        _thread.Start();
    }

    /// Every pad SDL currently reports, whether or not the configuration claims it. This is what the
    /// device picker lists, so a serial can be read off a connected pad instead of guessed.
    public IObservable<IReadOnlyList<SdlGamepadPresence>> WhenPresenceChanged => _presence;

    /// The same list as it stands now, for a caller that needs an answer rather than a subscription.
    public IReadOnlyList<SdlGamepadPresence> Present => _presence.Value;

    public SdlGamepadDevice Claim(string label, string? serialNumber, double deadzone, bool rumble)
    {
        var device = new SdlGamepadDevice(this, label, serialNumber, deadzone, rumble);
        Post(() =>
        {
            _devices.Add(device);
            Rebind();
        });
        return device;
    }

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
        _thread.Join();
        _stopping.Dispose();
        _presence.Dispose();
    }

    internal void Rumble(SdlGamepadDevice device, double intensity, TimeSpan duration) => Post(() =>
    {
        if (Claimed(device) is not { Info.SupportsRumble: true } pad)
        {
            return;
        }

        var amplitude = (ushort)(Math.Clamp(intensity, 0, 1) * ushort.MaxValue);
        SDL.RumbleGamepad(pad.Handle, amplitude, amplitude, (uint)duration.TotalMilliseconds);
    });

    internal Task ReleaseAsync(SdlGamepadDevice device)
    {
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Waiting on a thread that has already exited would never return, and a device disposed
        // after the system has stopped has nothing left to detach from anyway.
        if (_stopping.IsCancellationRequested)
        {
            released.SetResult();
            return released.Task;
        }

        Post(() =>
        {
            _devices.Remove(device);
            if (Claimed(device) is { } pad)
            {
                pad.ClaimedBy = null;
            }

            Rebind();
            released.SetResult();
        });
        return released.Task;
    }

    private void Post(Action work) => _work.Enqueue(work);

    private void Run()
    {
        // A camera desk is operated with the window in the background more often than not.
        SDL.SetHint(SDL.Hints.JoystickAllowBackgroundEvents, "1");
        if (!SDL.Init(SDL.InitFlags.Gamepad))
        {
            _logger.Error("SDL", $"gamepad support is unavailable - {SDL.GetError()}");
            return;
        }

        try
        {
            var connected = SDL.GetGamepads(out var count) ?? [];
            foreach (var instanceId in connected.Take(count))
            {
                Open(instanceId);
            }

            Pump();

            // A release posted while the application was shutting down still has to complete, or
            // whoever awaits it waits on a thread that is already gone.
            Drain();
        }
        finally
        {
            foreach (var pad in _pads.Values)
            {
                SDL.CloseGamepad(pad.Handle);
            }

            _pads.Clear();
            SDL.Quit();
        }
    }

    private void Pump()
    {
        while (!_stopping.IsCancellationRequested)
        {
            Drain();

            if (SDL.WaitEventTimeout(out var first, PollTimeoutMs))
            {
                Handle(first);
                while (SDL.PollEvent(out var next))
                {
                    Handle(next);
                }
            }
        }
    }

    private void Drain()
    {
        while (_work.TryDequeue(out var work))
        {
            work();
        }
    }

    private void Handle(SDL.Event received)
    {
        switch ((SDL.EventType)received.Type)
        {
            case SDL.EventType.GamepadAdded:
                Open(received.GDevice.Which);
                break;
            case SDL.EventType.GamepadRemoved:
                Close(received.GDevice.Which);
                break;
            case SDL.EventType.GamepadAxisMotion:
                Bound(received.GAxis.Which)?.Axis((SDL.GamepadAxis)received.GAxis.Axis, received.GAxis.Value);
                break;
            case SDL.EventType.GamepadButtonDown:
            case SDL.EventType.GamepadButtonUp:
                Bound(received.GButton.Which)?.Button((SDL.GamepadButton)received.GButton.Button, received.GButton.Down);
                break;
        }
    }

    private void Open(uint instanceId)
    {
        if (_pads.ContainsKey(instanceId))
        {
            return;
        }

        var handle = SDL.OpenGamepad(instanceId);
        if (handle == IntPtr.Zero)
        {
            _logger.Error("SDL", $"gamepad {instanceId} could not be opened - {SDL.GetError()}");
            return;
        }

        var properties = SDL.GetGamepadProperties(handle);
        var info = new SdlGamepadInfo(
            instanceId,
            SDL.GetGamepadName(handle) ?? "gamepad",
            Reported(SDL.GetGamepadSerial(handle)),
            SDL.GetGamepadPath(handle) ?? string.Empty,
            SDL.GetBooleanProperty(properties, SDL.Props.GamepadCapRumbleBoolean, false));

        _pads[instanceId] = new OpenPad(handle, info);

        // A pad appearing is new information, so a claim that still cannot match it is worth saying
        // again. Without this every replug on a busy desk reprints the whole unmatched list.
        foreach (var device in _devices)
        {
            device.ReportedUnmatched = false;
        }

        _logger.Log(
            "SDL",
            $"connected {info.Name}, serial {info.Serial ?? "not reported"}, rumble {(info.SupportsRumble ? "yes" : "no")}");
        Rebind();
    }

    private void Close(uint instanceId)
    {
        if (!_pads.Remove(instanceId, out var pad))
        {
            return;
        }

        pad.ClaimedBy?.Unbind();
        SDL.CloseGamepad(pad.Handle);
        _logger.Log("SDL", $"disconnected {pad.Info.Name}");
        Rebind();
    }

    private void Rebind()
    {
        var unbound = _devices.Where(device => device.Bound is null).ToList();
        var free = _pads.Values.Where(pad => pad.ClaimedBy is null).ToList();
        var assignment = GamepadClaims.Assign(
            unbound.Select(device => device.SerialNumber).ToList(),
            free.Select(pad => pad.Info.Serial).ToList());

        for (var claim = 0; claim < unbound.Count; claim++)
        {
            if (assignment[claim] is not { } index)
            {
                continue;
            }

            free[index].ClaimedBy = unbound[claim];
            unbound[claim].Bind(free[index].Info);
        }

        foreach (var device in unbound.Where(Unmatched))
        {
            device.ReportedUnmatched = true;
            _logger.Error("SDL", $"{device.Label} matches no connected pad with serial {device.SerialNumber}. Seen: {Seen()}");
        }

        _presence.OnNext(_pads.Values
            .Select(pad => new SdlGamepadPresence(pad.Info, pad.ClaimedBy?.Label))
            .ToList());
    }

    private static bool Unmatched(SdlGamepadDevice device) =>
        device is { Bound: null, SerialNumber: not null, ReportedUnmatched: false };

    private string Seen() => _pads.Count == 0
        ? "no pads are connected"
        : string.Join(", ", _pads.Values.Select(pad => pad.Info.Serial ?? $"{pad.Info.Name} (no serial)"));

    private OpenPad? Claimed(SdlGamepadDevice device) =>
        _pads.Values.FirstOrDefault(pad => pad.ClaimedBy == device);

    private SdlGamepadDevice? Bound(uint instanceId) =>
        _pads.TryGetValue(instanceId, out var pad) ? pad.ClaimedBy : null;

    private static string? Reported(string? serial) => string.IsNullOrWhiteSpace(serial) ? null : serial;

    private sealed class OpenPad(IntPtr handle, SdlGamepadInfo info)
    {
        public IntPtr Handle { get; } = handle;

        public SdlGamepadInfo Info { get; } = info;

        public SdlGamepadDevice? ClaimedBy { get; set; }
    }
}
