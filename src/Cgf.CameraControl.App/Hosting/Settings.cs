using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cgf.CameraControl.App.Hosting;

public sealed record AppSettings
{
    public string? ConfigPath { get; set; }

    public string? Language { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class AppSettingsJson : JsonSerializerContext;

/// What the application remembers between runs. A desk is configured once and then started by
/// whoever is on duty, so asking for the file and the language every time is asking for the wrong
/// one to be picked.
public static class Settings
{
    private static string File => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create),
        "CgfCameraControl",
        "settings.json");

    public static AppSettings Read()
    {
        try
        {
            using var stream = System.IO.File.OpenRead(File);
            return JsonSerializer.Deserialize(stream, AppSettingsJson.Default.AppSettings) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }
    }

    public static void Update(Action<AppSettings> change)
    {
        var settings = Read();
        change(settings);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(File)!);
            using var stream = System.IO.File.Create(File);
            JsonSerializer.Serialize(stream, settings, AppSettingsJson.Default.AppSettings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Forgetting the last session is not worth refusing to run.
        }
    }
}
