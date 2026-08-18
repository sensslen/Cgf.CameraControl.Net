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

    public void Log(string message) => Write(message, isError: false);

    public void Error(string message) => Write(message, isError: true);

    /// Every component prefixes its lines with `Component:` or `Component(which):`, which is what
    /// the log filter groups by. Which instance it was stays in the message, so filtering by
    /// component does not merge two cameras reporting the same failure into one indistinguishable
    /// pair of lines.
    private void Write(string message, bool isError)
    {
        var separator = SeparatorIn(message);
        if (separator < 0)
        {
            _entries.OnNext(new LogEntry(DateTimeOffset.Now, "Application", message, isError));
            return;
        }

        var prefix = message[..separator];
        var text = message[(separator + 1)..].Trim();
        var detail = prefix.IndexOfAny(['(', '[']);

        _entries.OnNext(detail < 0
            ? new LogEntry(DateTimeOffset.Now, prefix, text, isError)
            : new LogEntry(DateTimeOffset.Now, prefix[..detail], $"{prefix[detail..]} {text}", isError));
    }

    // The colon that ends the prefix, ignoring the ones inside a ws:// address.
    private static int SeparatorIn(string message)
    {
        var depth = 0;
        for (var index = 0; index < message.Length; index++)
        {
            switch (message[index])
            {
                case '(' or '[':
                    depth++;
                    break;
                case ')' or ']':
                    depth--;
                    break;
                case ':' when depth == 0:
                    return index;
            }
        }

        return -1;
    }
}
