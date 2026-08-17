using AtemSharp.Communication;
using AtemSharp.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Cgf.CameraControl.Atem;

public readonly record struct AotProbeResult(bool RegistryAlive, string Report);

// AtemSharp discovers its command types via Assembly.GetTypes(). Trimming can empty that registry
// with no error at all, so a published build has to prove the parser still decodes a real command.
// Keeping AtemSharp is what TrimmerRootAssembly in the app project is for.
public static class AotProbe
{
    public static async Task<AotProbeResult> Touch()
    {
        var services = new ServiceCollection().AddAtemSharp().BuildServiceProvider().GetRequiredService<IServices>();

        var parser = services.CreateCommandParser();
        var known = Describe(parser, "PrgI", [0, 0, 0, 1]);
        var unknown = Describe(parser, "ZZZZ", [0, 0, 0, 0]);

        await using var client = services.CreateAtemClient();

        var alive = known != "null" && known != unknown;
        var verdict = alive
            ? "OK: command registry survived trimming"
            : "FAIL: command registry is empty, trimming removed the command types";

        return new AotProbeResult(alive, $"parser PrgI -> {known}, ZZZZ -> {unknown}{Environment.NewLine}{verdict}");
    }

    private static string Describe(ICommandParser parser, string name, byte[] payload)
    {
        try
        {
            return parser.ParseCommand(name, payload)?.GetType().Name ?? "null";
        }
        catch (Exception ex)
        {
            return ex.GetType().Name;
        }
    }
}
