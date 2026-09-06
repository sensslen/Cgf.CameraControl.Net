using Cgf.CameraControl.Cameras.ViscaOverIp.Camera;

namespace Cgf.CameraControl.Cameras.ViscaOverIp.Tests;

public class ViscaPacketTests
{
    public class PanTilt
    {
        [Fact]
        public void CarriesBothAxesInOneCommand()
        {
            Assert.Equal<byte[]>([0x81, 0x01, 0x06, 0x01, 0x18, 0x14, 0x02, 0x01, 0xFF], ViscaPacket.PanTilt(24, 20));
        }

        [Theory]
        [InlineData(-1, 0x01)]
        [InlineData(0, 0x03)]
        [InlineData(1, 0x02)]
        public void PanDirectionIsLeftStopRight(int speed, byte expected)
        {
            Assert.Equal(expected, ViscaPacket.PanTilt(speed, 0)[6]);
        }

        // VISCA counts tilt downwards while the camera contract counts it upwards.
        [Theory]
        [InlineData(-1, 0x02)]
        [InlineData(0, 0x03)]
        [InlineData(1, 0x01)]
        public void TiltDirectionIsInverted(int speed, byte expected)
        {
            Assert.Equal(expected, ViscaPacket.PanTilt(0, speed)[7]);
        }

        // Zero is not a speed the camera accepts, and the direction byte is what stops the axis.
        [Fact]
        public void AStoppedAxisStillCarriesALegalSpeed()
        {
            var packet = ViscaPacket.PanTilt(0, 0);

            Assert.Equal(0x01, packet[4]);
            Assert.Equal(0x01, packet[5]);
        }

        [Fact]
        public void SpeedIsClampedToWhatEachAxisAccepts()
        {
            var packet = ViscaPacket.PanTilt(-99, 99);

            Assert.Equal(0x18, packet[4]);
            Assert.Equal(0x14, packet[5]);
        }
    }

    public class Lens
    {
        // The camera counts lens speed from zero, so the first step is the slowest zoom it has
        // rather than the second.
        [Theory]
        [InlineData(0, 0x00)]
        [InlineData(1, 0x20)]
        [InlineData(8, 0x27)]
        [InlineData(-1, 0x30)]
        [InlineData(-8, 0x37)]
        [InlineData(4, 0x23)]
        public void ZoomEncodesDirectionInTheHighNibbleAndSpeedInTheLow(int speed, byte expected)
        {
            Assert.Equal<byte[]>([0x81, 0x01, 0x04, 0x07, expected, 0xFF], ViscaPacket.Zoom(speed));
        }

        [Theory]
        [InlineData(0, 0x00)]
        [InlineData(1, 0x20)]
        [InlineData(8, 0x27)]
        [InlineData(-8, 0x37)]
        public void FocusUsesTheSameEncodingOnItsOwnOperation(int speed, byte expected)
        {
            Assert.Equal<byte[]>([0x81, 0x01, 0x04, 0x08, expected, 0xFF], ViscaPacket.Focus(speed));
        }

        [Fact]
        public void SpeedBeyondTheLensRangeIsClamped()
        {
            Assert.Equal(0x27, ViscaPacket.Zoom(99)[4]);
        }
    }

    public class Replies
    {
        [Theory]
        [InlineData(0x41, ViscaReply.Acknowledged)]
        [InlineData(0x61, ViscaReply.Failed)]
        [InlineData(0x38, ViscaReply.Other)]
        public void TheMessageTypeNibbleDecidesWhatAReplyIs(byte type, ViscaReply expected)
        {
            Assert.Equal(expected, ViscaPacket.Classify([0x90, type, 0x03, 0xFF]));
        }

        [Fact]
        public void ACompletionIsTheWholeReply()
        {
            Assert.Equal(ViscaReply.Completed, ViscaPacket.Classify([0x90, 0x52, 0xFF]));
        }

        // An inquiry is answered with a completion carrying the answer after it. Read as a plain
        // completion it would open the command gate for a command nobody has answered for.
        [Fact]
        public void AnInquiryAnswerIsNotACompletion()
        {
            Assert.Equal(
                ViscaReply.Other,
                ViscaPacket.Classify([0x90, 0x50, 0x00, 0x01, 0x02, 0x03, 0x04, 0xFF]));
        }

        [Fact]
        public void APacketWithoutATerminatorIsNotAReply()
        {
            Assert.Equal(ViscaReply.Other, ViscaPacket.Classify([0x90, 0x41, 0x00]));
        }

        [Fact]
        public void TwoRepliesInOneDatagramAreReadSeparately()
        {
            var packets = ViscaPacket.Split([0x90, 0x41, 0xFF, 0x90, 0x51, 0xFF]).ToList();

            Assert.Equal(2, packets.Count);
            Assert.Equal(ViscaReply.Acknowledged, ViscaPacket.Classify(packets[0]));
            Assert.Equal(ViscaReply.Completed, ViscaPacket.Classify(packets[1]));
        }

        [Fact]
        public void ATruncatedTailIsDroppedRatherThanMisread()
        {
            Assert.Single(ViscaPacket.Split([0x90, 0x41, 0xFF, 0x90]));
        }

        [Fact]
        public void TheErrorByteIsNamed()
        {
            Assert.Equal("command buffer full", ViscaPacket.Describe([0x90, 0x61, 0x03, 0xFF]));
        }
    }
}
