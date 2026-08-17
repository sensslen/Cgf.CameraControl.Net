namespace Cgf.CameraControl.Atem.Tests;

// Pins the payload layouts the rest of the ATEM tests are built on.
public class AtemStateDriverTests
{
    private readonly AtemStateDriver _driver = new();

    [Fact]
    public void ProgramInputLandsOnTheAddressedMixEffect()
    {
        _driver.ProgramInput(1, 4);

        var me = Assert.Single(_driver.State.Video.MixEffects, m => m.Id == 1);
        Assert.Equal(4, me.ProgramInput);
    }

    [Fact]
    public void PreviewInputLandsOnTheAddressedMixEffect()
    {
        _driver.PreviewInput(0, 3);

        var me = Assert.Single(_driver.State.Video.MixEffects, m => m.Id == 0);
        Assert.Equal(3, me.PreviewInput);
    }

    [Fact]
    public void KeyerOnAirLandsOnTheAddressedKeyer()
    {
        _driver.KeyerOnAir(0, 1, true);

        var me = Assert.Single(_driver.State.Video.MixEffects, m => m.Id == 0);
        var keyer = Assert.Single(me.UpstreamKeyers, k => k.Id == 1);
        Assert.True(keyer.OnAir);
    }

    // The negative on-air test would pass whether or not this landed, so it is pinned on its own.
    [Fact]
    public void KeyerFillSourceLandsOnTheAddressedKeyer()
    {
        _driver.KeyerFillSource(0, 1, 42);

        var me = Assert.Single(_driver.State.Video.MixEffects, m => m.Id == 0);
        var keyer = Assert.Single(me.UpstreamKeyers, k => k.Id == 1);
        Assert.Equal(42, keyer.FillSource);
    }

    [Fact]
    public void AuxSourceLandsOnTheAddressedOutput()
    {
        _driver.AuxSource(5, 16);

        var aux = Assert.Single(_driver.State.Video.Auxiliaries, a => a.Id == 5);
        Assert.Equal(16, aux.Source);
    }
}
