namespace Cgf.CameraControl.App.Hosting;

/// Remembers the configuration file across restarts. A desk is set up once and then started by
/// whoever is on duty, so asking for the file every time is asking for the wrong one to be picked.
public static class LastConfig
{
    private static string Marker => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create),
        "CgfCameraControl",
        "last-config.txt");

    public static string? Read()
    {
        try
        {
            var path = File.ReadAllText(Marker).Trim();
            return File.Exists(path) ? path : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static void Write(string path)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Marker)!);
            File.WriteAllText(Marker, path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Forgetting which file was open is not worth refusing to start over.
        }
    }
}
