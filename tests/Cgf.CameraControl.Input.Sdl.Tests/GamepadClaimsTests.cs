using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Sdl;

namespace Cgf.CameraControl.Input.Sdl.Tests;

public class GamepadClaimsTests
{
    public class Matching : GamepadClaimsTests
    {
        [Fact]
        public void NoConfiguredSerialTakesAnything()
        {
            Assert.True(GamepadClaims.Matches(null, "83234F94"));
            Assert.True(GamepadClaims.Matches(null, null));
        }

        [Theory]
        [InlineData("83234f94")]
        [InlineData("83-23-4F-94")]
        [InlineData(" 83234F94 ")]
        public void SerialsAreComparedWithoutCaseOrSeparators(string configured) =>
            Assert.True(GamepadClaims.Matches(configured, "83234F94"));

        [Fact]
        public void ADifferentSerialNeverMatches() =>
            Assert.False(GamepadClaims.Matches("83234F94", "CAD649D7"));

        // A pad SDL cannot read a serial from can never satisfy a configuration that names one, and
        // treating the absence as a wildcard would put an operator on the wrong desk.
        [Fact]
        public void APadWithoutASerialNeverSatisfiesAConfiguredOne() =>
            Assert.False(GamepadClaims.Matches("83234F94", null));
    }

    public class Assignment : GamepadClaimsTests
    {
        [Fact]
        public void AnAutoDetectingClaimTakesTheFirstPad() =>
            Assert.Equal([0], GamepadClaims.Assign([null], ["83234F94"]));

        [Fact]
        public void TwoAutoDetectingClaimsTakeOnePadEach() =>
            Assert.Equal([0, 1], GamepadClaims.Assign([null, null], ["83234F94", "CAD649D7"]));

        [Fact]
        public void ANamedClaimTakesThePadItNames() =>
            Assert.Equal([1], GamepadClaims.Assign(["CAD649D7"], ["83234F94", "CAD649D7"]));

        // ensi.json tells its two desks apart by serial. An auto-detecting interface enumerated
        // first must not swallow the one pad the named interface is waiting for, which is what
        // binding in configuration order would do here.
        [Fact]
        public void ANamedClaimIsServedBeforeAnAutoDetectingOne() =>
            Assert.Equal([null, 0], GamepadClaims.Assign([null, "83234F94"], ["83234F94"]));

        [Fact]
        public void AnUnmatchedNamedClaimStaysUnboundRatherThanTakingWhatIsThere() =>
            Assert.Equal([null], GamepadClaims.Assign(["83234F94"], ["CAD649D7"]));

        [Fact]
        public void MoreClaimsThanPadsLeavesTheRemainderUnbound() =>
            Assert.Equal([0, null], GamepadClaims.Assign([null, null], ["83234F94"]));

        [Fact]
        public void APadNobodyClaimsIsSimplyLeftAlone() =>
            Assert.Equal([0], GamepadClaims.Assign([null], ["83234F94", "CAD649D7"]));
    }
}
