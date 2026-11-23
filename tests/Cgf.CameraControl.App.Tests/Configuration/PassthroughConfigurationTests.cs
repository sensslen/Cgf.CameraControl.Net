using Cgf.CameraControl.Core.Logger;
using Cgf.CameraControl.Core.VideoMixer;
using Cgf.CameraControl.Core.VideoMixer.Passthrough;
using NSubstitute;

namespace Cgf.CameraControl.App.Tests.Configuration;

public class PassthroughConfigurationTests
{
    public class TallyPolicy : PassthroughConfigurationTests
    {
        [Fact]
        public async Task AnExistingMixerKeepsPreviewAndProgram()
        {
            var builder = new PassthroughBuilder(Substitute.For<ILogger>());
            await using var mixer = await Build(builder, Read("""{"type":"passthrough/default","instance":1}"""));
            Assert.Equal(MixerTallyMode.PreviewProgram, mixer.TallyMode);
        }

        [Theory]
        [InlineData("preview-program", MixerTallyMode.PreviewProgram)]
        [InlineData("program", MixerTallyMode.Program)]
        [InlineData("none", MixerTallyMode.None)]
        public async Task BuilderCarriesThePolicyWithoutFilteringSelection(string value, MixerTallyMode expected)
        {
            var builder = new PassthroughBuilder(Substitute.For<ILogger>());
            await using var mixer = await Build(builder, Read($$"""{"type":"passthrough/default","instance":1,"tallyMode":"{{value}}"}"""));
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
            await using var mixer = await Build(builder, Read("""
                {"type":"passthrough/default","instance":1, /* camera-only desk */ "tallyMode":"program",}
                """));
            Assert.Equal(MixerTallyMode.Program, mixer.TallyMode);
        }

        [Theory]
        [InlineData("\"invalid\"")]
        [InlineData("0")]
        [InlineData("\"2\"")]
        public void InvalidPoliciesReportTheMixerField(string value)
        {
            var problem = ConfigurationReader.Rejected(
                "videoMixers", $$"""{"type":"passthrough/default","instance":1,"tallyMode":{{value}}}""");
            Assert.StartsWith("videoMixers[0]: passthrough/default[1].tallyMode: ", problem, StringComparison.Ordinal);
        }
    }

    private static PassthroughConfiguration Read(string entry) =>
        ConfigurationReader.Read<PassthroughConfiguration>("videoMixers", entry);

    private static Task<IVideoMixer> Build(PassthroughBuilder builder, PassthroughConfiguration entry) =>
        builder.BuildAsync(entry, TestContext.Current.CancellationToken) ?? throw new InvalidOperationException("the builder did not claim the entry");
}
