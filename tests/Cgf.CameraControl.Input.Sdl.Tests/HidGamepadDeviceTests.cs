using System.Collections.Concurrent;
using System.Reactive.Linq;
using Cgf.CameraControl.Core.Logger;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Hid;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;
using NSubstitute;

namespace Cgf.CameraControl.Input.Sdl.Tests;

public class HidGamepadDeviceTests
{
    [Fact]
    public async Task ReconnectsToTheSameSerialAndResetsMovementOnRemoval()
    {
        var first = new Connection();
        var second = new Connection();
        var source = Substitute.For<IHidGamepadSource>();
        source.Open("83234F94").Returns(first, second);
        var logger = Substitute.For<ILogger>();
        await using var device = new HidGamepadDevice(source, "desk", "83234F94", 0, logger);
        var moved = Signal();
        var stopped = Signal();
        var reconnected = Signal();
        var connections = 0;
        using var movement = device.LeftStick.Subscribe(position =>
        {
            if (position.X == -1)
            {
                moved.TrySetResult();
            }
            else if (position == default && moved.Task.IsCompleted)
            {
                stopped.TrySetResult();
            }
        });
        using var connected = device.WhenConnectedChanged.Subscribe(value =>
        {
            if (value && Interlocked.Increment(ref connections) == 2)
            {
                reconnected.TrySetResult();
            }
        });
        first.Reports.Enqueue([0, 0, 128, 128, 128, 8, 0, 0, 0]);
        await Wait(moved);
        first.Disconnected = true;
        await Wait(stopped);
        await Wait(reconnected);
        source.Received(2).Open("83234F94");
        Assert.True(first.Disposed);
        Assert.False(device.SupportsRumble);
    }

    [Fact]
    public async Task AMalformedReportCostsOnlyThatReport()
    {
        var connection = new Connection();
        var source = Substitute.For<IHidGamepadSource>();
        source.Open("83234F94").Returns(connection);
        var logger = Substitute.For<ILogger>();
        await using var device = new HidGamepadDevice(source, "desk", "83234F94", 0, logger);
        var moved = Signal();
        using var movement = device.LeftStick.Subscribe(position =>
        {
            if (position.X == -1)
            {
                moved.TrySetResult();
            }
        });

        connection.Reports.Enqueue([0, 0, 128]);
        connection.Reports.Enqueue([0, 0, 128, 128, 128, 8, 0, 0, 0]);

        await Wait(moved);
        source.Received(1).Open("83234F94");
        logger.Received().Error("HID", Arg.Is<string>(message => message.Contains("report failed")));
    }

    [Fact]
    public async Task AThrowingSubscriberCostsOnlyThatReport()
    {
        var connection = new Connection();
        var source = Substitute.For<IHidGamepadSource>();
        source.Open("83234F94").Returns(connection);
        await using var device = new HidGamepadDevice(source, "desk", "83234F94", 0, Substitute.For<ILogger>());
        var moved = Signal();
        var thrown = false;
        using var movement = device.LeftStick.Subscribe(position =>
        {
            if (position.X != -1)
            {
                return;
            }

            if (!thrown)
            {
                thrown = true;
                throw new InvalidOperationException("camera gone");
            }

            moved.TrySetResult();
        });

        connection.Reports.Enqueue([0, 0, 128, 128, 128, 8, 0, 0, 0]);
        connection.Reports.Enqueue([0, 128, 128, 128, 128, 8, 0, 0, 0]);
        connection.Reports.Enqueue([0, 0, 128, 128, 128, 8, 0, 0, 0]);

        await Wait(moved);
    }

    [Fact]
    public async Task AThrowingStateSubscriberDoesNotSwallowTheMovement()
    {
        var connection = new Connection();
        var source = Substitute.For<IHidGamepadSource>();
        source.Open("83234F94").Returns(connection);
        await using var device = new HidGamepadDevice(source, "desk", "83234F94", 0, Substitute.For<ILogger>());
        var moved = Signal();
        var thrown = false;
        using var drawing = device.State.Subscribe(state =>
        {
            if (state.LeftStick.X == -1 && !thrown)
            {
                thrown = true;
                throw new InvalidOperationException("drawing gone");
            }
        });
        using var movement = device.LeftStick.Subscribe(position =>
        {
            if (position.X == -1)
            {
                moved.TrySetResult();
            }
        });

        connection.Reports.Enqueue([0, 0, 128, 128, 128, 8, 0, 0, 0]);
        connection.Reports.Enqueue([0, 0, 128, 128, 128, 8, 0, 0, 0]);

        await Wait(moved);
    }

    [Fact]
    public async Task MissingPadRemainsOfflineAndCanBeCancelledPromptly()
    {
        var source = Substitute.For<IHidGamepadSource>();
        source.Open("absent").Returns(_ => throw new IOException("absent"));
        var logger = Substitute.For<ILogger>();
        var reported = Signal();
        logger.When(log => log.Error("HID", Arg.Any<string>())).Do(_ => reported.TrySetResult());
        var device = new HidGamepadDevice(source, "desk", "absent", 0, logger);
        var connected = new List<bool>();
        using var subscription = device.WhenConnectedChanged.Subscribe(connected.Add);
        await Wait(reported);
        await device.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.DoesNotContain(true, connected);
    }

    [Fact]
    public async Task SerialCannotBeClaimedTwiceAndCanBeReclaimedAfterDisposal()
    {
        var source = Substitute.For<IHidGamepadSource>();
        source.Open(Arg.Any<string>()).Returns(_ => new Connection());
        await using var system = new HidGamepadSystem(Substitute.For<ILogger>(), source);
        var first = system.Claim("first", "83234F94", 0);
        Assert.Throws<InvalidOperationException>(() => system.Claim("second", "8323-4f94", 0));
        await first.DisposeAsync();
        await using var next = system.Claim("second", "83234F94", 0);
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static Task Wait(TaskCompletionSource signal) =>
        signal.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

    private sealed class Connection : IHidGamepadConnection
    {
        public ConcurrentQueue<byte[]> Reports { get; } = new();
        public volatile bool Disconnected;
        public bool Disposed { get; private set; }
        public string Description => "F310";

        public int Read(byte[] buffer)
        {
            if (Disconnected)
            {
                throw new IOException("unplugged");
            }
            if (Reports.TryDequeue(out var report))
            {
                report.CopyTo(buffer, 0);
                return report.Length;
            }
            Thread.Sleep(10);
            return 0;
        }

        public void Dispose() => Disposed = true;
    }
}
