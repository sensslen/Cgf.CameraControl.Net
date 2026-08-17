namespace Cgf.CameraControl.Core.Configuration;

public sealed class ConfigValidationException(string path, string message)
    : Exception($"{path}: {message}")
{
    public string Path { get; } = path;
}
