using System.CommandLine;
using Avalonia;

namespace Cgf.CameraControl.App;

internal sealed class Program
{
    // Everything a user needs is in the window. The only switches are the config file to open and a
    // release-gate probe CI runs against each published binary.
    [STAThread]
    public static int Main(string[] args)
    {
        var configOption = new Option<FileInfo?>("--config")
        {
            Description = "Configuration file to open instead of the last used one.",
        };

        var aotProbeOption = new Option<bool>("--aot-probe")
        {
            Description = "Verify the trimmed build still decodes ATEM commands, then exit.",
            Hidden = true,
        };

        var root = new RootCommand("Gamepad control for ATEM switchers and their cameras.")
        {
            configOption,
            aotProbeOption,
        };

        root.SetAction(parseResult => parseResult.GetValue(aotProbeOption)
            ? AotProbes.Run()
            : Launch(parseResult.GetValue(configOption), args));

        return root.Parse(args).Invoke();
    }

    private static int Launch(FileInfo? config, string[] args)
    {
        AppEnvironment.ConfigFile = config;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
