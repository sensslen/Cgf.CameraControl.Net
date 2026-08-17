using Cgf.CameraControl.Atem.VideoMixer.Blackmagicdesign;
using Cgf.CameraControl.Core.Configuration;

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

    private static AtemConfiguration Read(string json)
    {
        var config = ConfigLoader.Load($$"""{ "videoMixers": [ {{json}} ] }""", out _);
        return Assert.Single(config.VideoMixers).Deserialize(AtemConfigurationContext.Default.AtemConfiguration);
    }
}
