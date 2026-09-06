using AtemSharp.DependencyInjection;
using Cgf.CameraControl.Atem.VideoMixer.Blackmagicdesign;
using Cgf.CameraControl.Cameras.SignalrPtzLanc.Camera;
using Cgf.CameraControl.Cameras.ViscaOverIp.Camera;
using Cgf.CameraControl.Cameras.WebsocketPtzLanc.Camera;
using Cgf.CameraControl.Core;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.GenericFactory;
using Cgf.CameraControl.Core.VideoMixer.Passthrough;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Sdl;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Cgf.CameraControl.App.Hosting;

public sealed record ConfigLoadResult(
    string Path,
    IReadOnlyList<string> FileIssues,
    IReadOnlyList<LoadIssue> EntryIssues)
{
    public bool IsClean => FileIssues.Count == 0 && EntryIssues.Count == 0;
}

/// The composition root. Builders are registered once and survive a reload, because the core keeps
/// them when it tears its instances down.
public sealed class AppHost : IAsyncDisposable
{
    private readonly List<ObservedCamera> _cameras = [];
    private readonly List<ControlSurfaceDevice> _surfaces = [];

    public AppHost()
    {
        Gamepads = new SdlGamepadSystem(Logger);
        Core = new CameraControlCore(Logger);

        var atemServices = new ServiceCollection().AddAtemSharp().BuildServiceProvider().GetRequiredService<IServices>();

        Core.CameraFactory.AddBuilder(new ObservingCameraBuilder(new WebsocketPtzLancCameraBuilder(Logger), _cameras));
        Core.CameraFactory.AddBuilder(new ObservingCameraBuilder(new SignalrPtzLancCameraBuilder(Logger), _cameras));
        Core.CameraFactory.AddBuilder(new ObservingCameraBuilder(new ViscaOverIpCameraBuilder(Logger), _cameras));
        Core.MixerFactory.AddBuilder(new AtemBuilder(Logger, atemServices));
        Core.MixerFactory.AddBuilder(new PassthroughBuilder(Logger));
        Core.HmiFactory.AddBuilder(new GamepadBuilder(Gamepads, Core.MixerFactory, Core.CameraFactory, Logger));
        Core.HmiFactory.AddBuilder(new KeyboardBuilder(Core.MixerFactory, Core.CameraFactory, _surfaces, Logger));
    }

    public UiLogger Logger { get; } = new();

    public SdlGamepadSystem Gamepads { get; }

    public CameraControlCore Core { get; }

    public IReadOnlyList<ObservedCamera> Cameras => _cameras;

    /// The keyboard and mouse surfaces the configuration asked for, in the order they were built,
    /// so the window can draw one for each.
    public IReadOnlyList<ControlSurfaceDevice> Surfaces => _surfaces;

    public RootConfig Configuration { get; private set; } = RootConfig.Empty;

    public string? ConfigPath { get; private set; }

    public async Task<ConfigLoadResult> LoadAsync(string path, CancellationToken cancellationToken)
    {
        RootConfig config;
        IReadOnlyList<string> fileIssues;
        try
        {
            config = ConfigLoader.LoadFile(path, out fileIssues);
        }
        catch (Exception ex) when (ex is ConfigFormatException or IOException or UnauthorizedAccessException)
        {
            Logger.Error("Configuration", ex.Message);
            return new ConfigLoadResult(path, [ex.Message], []);
        }

        foreach (var issue in fileIssues)
        {
            Logger.Error("Configuration", issue);
        }

        _cameras.Clear();
        _surfaces.Clear();
        var entryIssues = await Core.ReconfigureAsync(config, cancellationToken).ConfigureAwait(false);

        Configuration = config;
        ConfigPath = path;
        Settings.Update(settings => settings.ConfigPath = path);
        Logger.Log("Configuration", $"loaded {path}");
        return new ConfigLoadResult(path, fileIssues, entryIssues);
    }

    /// Everything the configuration built, taken down, with the builders kept. Edit mode runs with the
    /// desk stopped: a configuration being rewritten is not one anything should be acting on, and a pad
    /// left in a pocket must not move a camera while its bindings are being changed.
    ///
    /// The pads themselves stay open. Nothing consumes them once the interfaces are gone, which is what
    /// lets the editor read a press to find out which control was meant.
    public async Task UnloadAsync(CancellationToken cancellationToken)
    {
        _cameras.Clear();
        _surfaces.Clear();
        await Core.ReconfigureAsync(RootConfig.Empty, cancellationToken).ConfigureAwait(false);
    }

    /// Writes the configuration and says whether it landed. The caller loads the file afterwards rather
    /// than trusting what it just wrote, so what runs is always what is on disk.
    public bool Save(RootConfig config, string path)
    {
        try
        {
            ConfigWriter.WriteFile(path, config);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Error("Configuration", $"could not write {path}: {ex.Message}");
            return false;
        }

        Logger.Log("Configuration", $"wrote {path}");
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        await Core.DisposeAsync().ConfigureAwait(false);
        await Gamepads.DisposeAsync().ConfigureAwait(false);
    }
}
