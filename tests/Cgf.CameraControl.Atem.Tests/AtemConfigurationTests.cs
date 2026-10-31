using Cgf.CameraControl.Atem.VideoMixer.Blackmagicdesign;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.VideoMixer;

namespace Cgf.CameraControl.Atem.Tests;

public class AtemConfigurationTests
{
    [Fact]
    public void ReadsTheShippedShape()
    {
        var config = Read("""{ "type": "blackmagicdesign/atem", "instance": 2, "ip": "10.0.0.241", "mixEffectBlock": 1 }""");

        Assert.Equal("10.0.0.241", config.Ip);
        Assert.Equal(1, config.MixEffectBlock);
    }

    [Theory]
    [InlineData("atem.local")]
    [InlineData("fe80::1")]
    public void AcceptsAHostNameOrIpv6Literal(string host)
    {
        Assert.Equal(host, Read($$"""{ "type": "blackmagicdesign/atem", "instance": 1, "ip": "{{host}}", "mixEffectBlock": 0 }""").Ip);
    }

    [Fact]
    public void RequiresIp()
    {
        var error = Assert.Throws<ConfigValidationException>(() =>
            Read("""{ "type": "blackmagicdesign/atem", "instance": 1, "mixEffectBlock": 0 }"""));

        Assert.Contains("'ip'", error.Message, StringComparison.Ordinal);
    }

    // The converter runs while reading, so the reported path comes from the reader rather than from
    // anything the validation has to reconstruct.
    [Fact]
    public void RejectsAnAddressThatIsNeitherHostNorIp()
    {
        var error = Assert.Throws<ConfigValidationException>(() =>
            Read("""{ "type": "blackmagicdesign/atem", "instance": 1, "ip": "10.0.0.1 ", "mixEffectBlock": 0 }"""));

        Assert.Equal("blackmagicdesign/atem[1].ip", error.Path);
        Assert.Contains("host name or IP address", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsANegativeMixEffectBlock()
    {
        var error = Assert.Throws<ConfigValidationException>(() =>
            Read("""{ "type": "blackmagicdesign/atem", "instance": 1, "ip": "10.0.0.1", "mixEffectBlock": -1 }"""));

        Assert.Equal("blackmagicdesign/atem[1].mixEffectBlock", error.Path);
        Assert.Contains("non-negative", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAFractionalMixEffectBlock()
    {
        Assert.Throws<ConfigValidationException>(() =>
            Read("""{ "type": "blackmagicdesign/atem", "instance": 1, "ip": "10.0.0.1", "mixEffectBlock": 1.5 }"""));
    }

    public class TallyPolicy : AtemConfigurationTests
    {
        [Fact]
        public void ExistingConfigurationsKeepPreviewAndProgram()
        {
            Assert.Equal(MixerTallyMode.PreviewProgram,
                Read("""{ "type": "blackmagicdesign/atem", "instance": 1, "ip": "10.0.0.1", "mixEffectBlock": 0 }""").TallyMode);
        }

        [Theory]
        [InlineData("preview-program", MixerTallyMode.PreviewProgram)]
        [InlineData("program", MixerTallyMode.Program)]
        [InlineData("none", MixerTallyMode.None)]
        public void ReadsTheSelectedPolicy(string value, MixerTallyMode expected)
        {
            Assert.Equal(expected, Read($$"""
                { "type": "blackmagicdesign/atem", "instance": 1, "ip": "10.0.0.1", "mixEffectBlock": 0, "tallyMode": "{{value}}" }
                """).TallyMode);
        }

        [Theory]
        [InlineData("\"invalid\"")]
        [InlineData("0")]
        [InlineData("\"2\"")]
        [InlineData("null")]
        public void InvalidPoliciesReportTheMixerField(string value)
        {
            var error = Assert.Throws<ConfigValidationException>(() => Read($$"""
                { "type": "blackmagicdesign/atem", "instance": 1, "ip": "10.0.0.1", "mixEffectBlock": 0, "tallyMode": {{value}} }
                """));
            Assert.Equal("blackmagicdesign/atem[1].tallyMode", error.Path);
        }
    }

    private static AtemConfiguration Read(string json)
    {
        var config = ConfigLoader.Load($$"""{ "videoMixers": [ {{json}} ] }""", out _);
        return Assert.Single(config.VideoMixers).Deserialize(AtemConfigurationContext.Default.AtemConfiguration);
    }
}
