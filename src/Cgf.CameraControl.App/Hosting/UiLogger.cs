using System.Reactive.Subjects;
using Cgf.CameraControl.Core.Logger;

namespace Cgf.CameraControl.App.Hosting;

public sealed record LogEntry(DateTimeOffset At, string Source, string Message, bool IsError)
{
    public string Time => At.ToString("HH:mm:ss");
}

/// Log lines arrive from the SDL thread, the websocket readers and the ATEM dataflow blocks, so the
/// subject is the only synchronisation point and the view models marshal from there.
public sealed class UiLogger : ILogger
{
    private readonly Subject<LogEntry> _entries = new();

    public IObservable<LogEntry> WhenLogged => _entries;

    public void Log(string source, string message) => Write(source, message, isError: false);

    public void Error(string source, string message) => Write(source, message, isError: true);

    /// The source is what the filter groups by, so which instance reported it stays in the message:
    /// grouping by component must not merge two cameras reporting the same failure into one
    /// indistinguishable pair of lines.
    private void Write(string source, string message, bool isError) =>
        _entries.OnNext(new LogEntry(DateTimeOffset.Now, source, message, isError));
}
