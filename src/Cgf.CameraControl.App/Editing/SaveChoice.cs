namespace Cgf.CameraControl.App.Editing;

/// The three ways out of edit mode. Cancel is here because a save that cannot happen, and a discard
/// that throws the work away, must not be the only two answers to a question the operator did not
/// mean to ask.
public enum SaveChoice
{
    Save,
    Discard,
    Cancel,
}
