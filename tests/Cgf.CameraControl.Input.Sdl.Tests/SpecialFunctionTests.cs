using Cgf.CameraControl.Core.VideoMixer;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.ConnectionChange;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.SpecialFunctions;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.SpecialFunctions.Macro;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.SpecialFunctions.Macro.Toggle;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Cgf.CameraControl.Input.Sdl.Tests;

public class SpecialFunctionTests
{
    private readonly IVideoMixer _mixer = Substitute.For<IVideoMixer>();

    [Fact]
    public async Task KeyTogglesTheConfiguredKeyer()
    {
        await Run(new KeySpecialFunctionConfiguration { Index = 3 });

        _mixer.Received(1).ToggleKey(3);
    }

    [Fact]
    public async Task ConnectionChangeSelectsTheConfiguredInput()
    {
        await Run(new ConnectionChangeSpecialFunctionConfiguration { Index = 5 });

        _mixer.Received(1).ChangeInput(5);
    }

    public class MacroLoop : SpecialFunctionTests
    {
        [Fact]
        public async Task StepsThroughTheMacrosAndWrapsAround()
        {
            var function = SpecialFunctionFactory.Get(new MacroLoopSpecialFunctionConfiguration { Indexes = [7, 8, 9] });

            for (var i = 0; i < 4; i++)
            {
                await function.RunAsync(_mixer, TestContext.Current.CancellationToken);
            }

            Received.InOrder(() =>
            {
                _mixer.RunMacro(7);
                _mixer.RunMacro(8);
                _mixer.RunMacro(9);
                _mixer.RunMacro(7);
            });
        }

        [Fact]
        public async Task ASingleMacroRepeats()
        {
            var function = SpecialFunctionFactory.Get(new MacroLoopSpecialFunctionConfiguration { Indexes = [4] });

            await function.RunAsync(_mixer, TestContext.Current.CancellationToken);
            await function.RunAsync(_mixer, TestContext.Current.CancellationToken);

            _mixer.Received(2).RunMacro(4);
        }
    }

    public class MacroToggle : SpecialFunctionTests
    {
        [Fact]
        public async Task RunsTheOffMacroWhenTheKeyIsAlreadySet()
        {
            _mixer.IsKeySetAsync(0, Arg.Any<CancellationToken>()).Returns(true);

            await Run(Toggle(new KeyConditionConfiguration { Key = 0 }));

            _mixer.Received(1).RunMacro(24);
        }

        [Fact]
        public async Task RunsTheOnMacroWhenTheKeyIsClear()
        {
            _mixer.IsKeySetAsync(0, Arg.Any<CancellationToken>()).Returns(false);

            await Run(Toggle(new KeyConditionConfiguration { Key = 0 }));

            _mixer.Received(1).RunMacro(23);
        }

        [Fact]
        public async Task ComparesTheAuxiliarySelection()
        {
            _mixer.GetAuxiliarySelectionAsync(5, Arg.Any<CancellationToken>()).Returns(16);

            await Run(Toggle(new AuxSelectionConditionConfiguration { Aux = 5, Selection = 16 }));

            _mixer.Received(1).RunMacro(24);
        }

        [Fact]
        public async Task ADifferentAuxiliarySelectionCountsAsInactive()
        {
            _mixer.GetAuxiliarySelectionAsync(5, Arg.Any<CancellationToken>()).Returns(1);

            await Run(Toggle(new AuxSelectionConditionConfiguration { Aux = 5, Selection = 16 }));

            _mixer.Received(1).RunMacro(23);
        }

        // Guessing which way to toggle would run the wrong macro on air.
        [Fact]
        public async Task AnUnanswerableConditionRunsNothing()
        {
            _mixer.GetAuxiliarySelectionAsync(9, Arg.Any<CancellationToken>())
                .Throws(new InvalidOperationException("the switcher has no auxiliary 9"));

            await Run(Toggle(new AuxSelectionConditionConfiguration { Aux = 9, Selection = 1 }));

            _mixer.DidNotReceive().RunMacro(Arg.Any<int>());
        }

        private static MacroToggleSpecialFunctionConfiguration Toggle(MacroToggleConditionConfiguration condition) =>
            new() { IndexOn = 23, IndexOff = 24, Condition = condition };
    }

    private Task Run(SpecialFunctionConfiguration config) =>
        SpecialFunctionFactory.Get(config).RunAsync(_mixer, TestContext.Current.CancellationToken);
}
