using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.Logger;
using Cgf.CameraControl.Core.VideoMixer;
using Cgf.CameraControl.Core.VideoMixer.Passthrough;
using NSubstitute;

namespace Cgf.CameraControl.Core.Tests;

public class PassthroughConfigurationTests
{
    public class TallyPolicy : PassthroughConfigurationTests
    {
        [Fact]
        public async Task AnExistingMixerKeepsPreviewAndProgram()
        {
            var builder = new PassthroughBuilder(Substitute.For<ILogger>());
            await using var mixer = await builder.BuildAsync(
                Read("""{"type":"passthrough/default","instance":1}"""), TestContext.Current.CancellationToken);
            Assert.Equal(MixerTallyMode.PreviewProgram, mixer.TallyMode);
        }

        [Theory]
        [InlineData("preview-program", MixerTallyMode.PreviewProgram)]
        [InlineData("program", MixerTallyMode.Program)]
        [InlineData("none", MixerTallyMode.None)]
        public async Task BuilderCarriesThePolicyWithoutFilteringSelection(string value, MixerTallyMode expected)
        {
            var builder = new PassthroughBuilder(Substitute.For<ILogger>());
            await using var mixer = await builder.BuildAsync(
                Read($$"""{"type":"passthrough/default","instance":1,"tallyMode":"{{value}}"}"""), TestContext.Current.CancellationToken);
            var preview = new List<PreviewChange>();
            var program = new List<int>();
            using var previewSubscription = mixer.WhenPreviewChanged.Subscribe(preview.Add);
            using var programSubscription = mixer.WhenProgramChanged.Subscribe(program.Add);
            mixer.ChangeInput(2);
            mixer.Cut();

            Assert.Equal(expected, mixer.TallyMode);
            Assert.Equal(2, program[^1]);
            Assert.Equal(-1, preview[^1].Preview);
        }

        [Fact]
        public async Task CommentsAndTrailingCommasRemainSupported()
        {
            var builder = new PassthroughBuilder(Substitute.For<ILogger>());
            await using var mixer = await builder.BuildAsync(Read("""
                {"type":"passthrough/default","instance":1, /* camera-only desk */ "tallyMode":"program",}
                """), TestContext.Current.CancellationToken);
            Assert.Equal(MixerTallyMode.Program, mixer.TallyMode);
        }

        [Theory]
        [InlineData("\"invalid\"")]
        [InlineData("0")]
        [InlineData("\"2\"")]
        [InlineData("null")]
        public async Task InvalidPoliciesReportTheMixerField(string value)
        {
            var builder = new PassthroughBuilder(Substitute.For<ILogger>());
            var error = await Assert.ThrowsAsync<ConfigValidationException>(() => builder.BuildAsync(
                Read($$"""{"type":"passthrough/default","instance":1,"tallyMode":{{value}}}"""), TestContext.Current.CancellationToken));
            Assert.Equal("passthrough/default[1].tallyMode", error.Path);
        }
    }

    private static ConfigEntry Read(string entry)
    {
        var config = ConfigLoader.Load($$"""
            {"videoMixers":[{{entry}}]}
            """, out _);
        return Assert.Single(config.VideoMixers);
    }
}
