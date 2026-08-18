using Cgf.CameraControl.Core.Logger;
using Cgf.CameraControl.Core.VideoMixer;
using Cgf.CameraControl.Core.VideoMixer.Passthrough;
using NSubstitute;

namespace Cgf.CameraControl.Core.Tests;

public class PassthroughTests
{
    private readonly Passthrough _mixer = new(Substitute.For<ILogger>());

    [Fact]
    public void NothingIsSelectedBeforeAnythingHappens()
    {
        Assert.Equal(new PreviewChange(-1, false), Latest(_mixer.WhenPreviewChanged));
        Assert.Equal(-1, Latest(_mixer.WhenProgramChanged));
    }

    [Fact]
    public void ChangingInputMovesPreviewAndLeavesProgramAlone()
    {
        _mixer.ChangeInput(4);

        Assert.Equal(new PreviewChange(4, false), Latest(_mixer.WhenPreviewChanged));
        Assert.Equal(-1, Latest(_mixer.WhenProgramChanged));
    }

    // The buses swap, so the desk an operator was cutting away from is the one they can steer next.
    [Fact]
    public void ATransitionSwapsTheBuses()
    {
        _mixer.ChangeInput(1);
        _mixer.Cut();
        _mixer.ChangeInput(2);

        _mixer.Auto();

        Assert.Equal(2, Latest(_mixer.WhenProgramChanged));
        Assert.Equal(new PreviewChange(1, false), Latest(_mixer.WhenPreviewChanged));
    }

    private static T Latest<T>(IObservable<T> source)
    {
        T? latest = default;
        using (source.Subscribe(value => latest = value))
        {
            return latest!;
        }
    }
}
