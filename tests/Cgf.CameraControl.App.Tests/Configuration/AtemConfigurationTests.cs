using Cgf.CameraControl.Atem.VideoMixer.Blackmagicdesign;
using Cgf.CameraControl.Core.VideoMixer;

namespace Cgf.CameraControl.App.Tests.Configuration;

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
        var error = Rejected("""{ "type": "blackmagicdesign/atem", "instance": 1, "mixEffectBlock": 0 }""");

        Assert.Contains("'ip'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAnAddressThatIsNeitherHostNorIp()
    {
        var error = Rejected("""{ "type": "blackmagicdesign/atem", "instance": 1, "ip": "10.0.0.1 ", "mixEffectBlock": 0 }""");

        Assert.Contains("blackmagicdesign/atem[1].ip: ", error, StringComparison.Ordinal);
        Assert.Contains("host name or IP address", error, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsANegativeMixEffectBlock()
    {
        var error = Rejected("""{ "type": "blackmagicdesign/atem", "instance": 1, "ip": "10.0.0.1", "mixEffectBlock": -1 }""");

        Assert.Contains("blackmagicdesign/atem[1].mixEffectBlock: ", error, StringComparison.Ordinal);
        Assert.Contains("must be between 0 and", error, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAFractionalMixEffectBlock()
    {
        Rejected("""{ "type": "blackmagicdesign/atem", "instance": 1, "ip": "10.0.0.1", "mixEffectBlock": 1.5 }""");
    }

    public class TallyPolicy : AtemConfigurationTests
    {
        [Fact]
        public void AnAbsentPolicyIsLeftUnset()
        {
            Assert.Null(Read("""{ "type": "blackmagicdesign/atem", "instance": 1, "ip": "10.0.0.1", "mixEffectBlock": 0 }""").TallyMode);
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
        public void InvalidPoliciesReportTheMixerField(string value)
        {
            var error = Rejected($$"""
                { "type": "blackmagicdesign/atem", "instance": 1, "ip": "10.0.0.1", "mixEffectBlock": 0, "tallyMode": {{value}} }
                """);
            Assert.Contains("blackmagicdesign/atem[1].tallyMode: ", error, StringComparison.Ordinal);
        }
    }

    private static AtemConfiguration Read(string json) => ConfigurationReader.Read<AtemConfiguration>("videoMixers", json);

    private static string Rejected(string json) => ConfigurationReader.Rejected("videoMixers", json);
}
