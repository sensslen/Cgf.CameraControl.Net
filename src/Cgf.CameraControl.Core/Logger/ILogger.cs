namespace Cgf.CameraControl.Core.Logger;

/// A line and the thing that produced it, kept apart. The log is grouped and filtered by source, and
/// a source recovered by looking for a colon in a sentence is a source that breaks the first time a
/// message contains one.
public interface ILogger
{
    void Log(string source, string message);

    void Error(string source, string message);
}
