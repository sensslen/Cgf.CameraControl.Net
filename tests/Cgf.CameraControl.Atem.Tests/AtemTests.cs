using AtemSharp.Commands.MixEffects;
using AtemSharp.Commands.MixEffects.Key;
using AtemSharp.Commands.MixEffects.Transition;
using Cgf.CameraControl.Atem.VideoMixer.Blackmagicdesign;
using Cgf.CameraControl.Core.VideoMixer;

namespace Cgf.CameraControl.Atem.Tests;

public class AtemTests
{
    private readonly FakeAtemConnection _connection = new();

    [Fact]
    public void ConnectionStringNamesTheBlock()
    {
        Assert.Equal("10.0.0.1:1", Build(mixEffectBlock: 1).ConnectionString);
    }

    public class Selection : AtemTests
    {
        [Fact]
        public void PreviewChangeReportsTheNewInput()
        {
            var (atem, seen) = Observe();

            _connection.Push(d => d.PreviewInput(0, 3));

            Assert.Equal(new PreviewChange(3, false), seen[^1]);
            _ = atem;
        }

        [Fact]
        public void PreviewIsOnAirWhenItIsAlsoTheProgramInput()
        {
            var (_, seen) = Observe();

            _connection.Push(d => d.ProgramInput(0, 3));
            _connection.Push(d => d.PreviewInput(0, 3));

            Assert.Equal(new PreviewChange(3, true), seen[^1]);
        }

        [Fact]
        public void PreviewIsOnAirWhenAKeyerOnAirFillsFromIt()
        {
            var (_, seen) = Observe();

            _connection.Push(d => d.ProgramInput(0, 1));
            _connection.Push(d => d.KeyerFillSource(0, 0, 4));
            _connection.Push(d => d.KeyerOnAir(0, 0, true));
            _connection.Push(d => d.PreviewInput(0, 4));

            Assert.Equal(new PreviewChange(4, true), seen[^1]);
        }

        [Fact]
        public void AKeyerThatIsNotOnAirDoesNotPutItsFillOnAir()
        {
            var (_, seen) = Observe();

            _connection.Push(d => d.ProgramInput(0, 1));
            _connection.Push(d => d.KeyerFillSource(0, 0, 4));
            _connection.Push(d => d.PreviewInput(0, 4));

            Assert.Equal(new PreviewChange(4, false), seen[^1]);
        }

        [Fact]
        public void RepeatedStateWithTheSameSelectionEmitsOnce()
        {
            var (_, seen) = Observe();
            var before = seen.Count;

            _connection.Push(d => d.PreviewInput(0, 3));
            _connection.Push(d => d.PreviewInput(0, 3));
            _connection.Push(d => d.PreviewInput(0, 3));

            Assert.Equal(before + 1, seen.Count);
        }

        [Fact]
        public void ProgramChangeIsReportedSeparatelyAndOnlyWhenItMoves()
        {
            var atem = Build();
            var seen = new List<int>();
            using var _ = atem.WhenProgramChanged.Subscribe(seen.Add);
            var before = seen.Count;

            _connection.Push(d => d.ProgramInput(0, 2));
            _connection.Push(d => d.ProgramInput(0, 2));

            Assert.Equal(before + 1, seen.Count);
            Assert.Equal(2, seen[^1]);
        }

        // ensi.json runs two blocks off one switcher, so a block must report its own selection and
        // never the other one's.
        [Fact]
        public void AnotherBlocksSelectionIsNeverReported()
        {
            var (_, seen) = Observe(mixEffectBlock: 1);

            _connection.Push(d => d.PreviewInput(0, 7));
            _connection.Push(d => d.PreviewInput(1, 2));

            Assert.DoesNotContain(seen, change => change.Preview == 7);
            Assert.Equal(new PreviewChange(2, false), seen[^1]);
        }
    }

    public class Commands : AtemTests
    {
        [Fact]
        public void ChangeInputSendsAPreviewInputCommand()
        {
            Build().ChangeInput(5);

            Assert.IsType<PreviewInputCommand>(Assert.Single(_connection.Sent));
        }

        [Fact]
        public void CutAndAutoSendTheirTransitionCommands()
        {
            var atem = Build();

            atem.Cut();
            atem.Auto();

            Assert.Collection(
                _connection.Sent,
                command => Assert.IsType<CutCommand>(command),
                command => Assert.IsType<AutoTransitionCommand>(command));
        }

        [Fact]
        public void ToggleKeyInvertsWhateverTheKeyerCurrentlyIs()
        {
            var atem = Build();
            _connection.Push(d => d.KeyerOnAir(0, 1, true));

            atem.ToggleKey(1);

            var command = Assert.IsType<MixEffectKeyOnAirCommand>(Assert.Single(_connection.Sent));
            Assert.False(command.OnAir);
        }

        [Fact]
        public void NothingIsSentWhileDisconnected()
        {
            var atem = Build();
            _connection.Connected = false;

            atem.Cut();
            atem.Auto();
            atem.ChangeInput(2);
            atem.ToggleKey(0);
            atem.RunMacro(1);

            Assert.Empty(_connection.Sent);
        }
    }

    public class Queries : AtemTests
    {
        [Fact]
        public async Task IsKeySetReadsTheKeyer()
        {
            var atem = Build();
            _connection.Push(d => d.KeyerOnAir(0, 2, true));

            Assert.True(await atem.IsKeySetAsync(2, TestContext.Current.CancellationToken));
            Assert.False(await atem.IsKeySetAsync(3, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task AuxiliarySelectionReadsTheOutput()
        {
            var atem = Build();
            _connection.Push(d => d.AuxSource(5, 16));

            Assert.Equal(16, await atem.GetAuxiliarySelectionAsync(5, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task AnUnknownAuxiliaryIsAnError()
        {
            var atem = Build();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => atem.GetAuxiliarySelectionAsync(99, TestContext.Current.CancellationToken));
        }
    }

    public class Teardown : AtemTests
    {
        [Fact]
        public async Task DisposeReleasesTheSharedConnection()
        {
            await Build().DisposeAsync();

            Assert.Equal("10.0.0.1", Assert.Single(_connection.Released));
        }
    }

    private Atem.VideoMixer.Blackmagicdesign.Atem Build(int mixEffectBlock = 0) =>
        new(new AtemConfiguration { Ip = "10.0.0.1", MixEffectBlock = mixEffectBlock }, _connection);

    private (Atem.VideoMixer.Blackmagicdesign.Atem Atem, List<PreviewChange> Seen) Observe(int mixEffectBlock = 0)
    {
        var atem = Build(mixEffectBlock);
        var seen = new List<PreviewChange>();
        atem.WhenPreviewChanged.Subscribe(seen.Add);
        return (atem, seen);
    }
}
