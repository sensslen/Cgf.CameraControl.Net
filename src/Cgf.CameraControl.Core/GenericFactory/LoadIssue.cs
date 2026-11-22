using Cgf.CameraControl.Core.Configuration;

namespace Cgf.CameraControl.Core.GenericFactory;

public sealed record LoadIssue(string Section, EntryConfiguration Entry, string Message)
{
    public override string ToString() => $"{Section}: {Entry} - {Message}";
}
