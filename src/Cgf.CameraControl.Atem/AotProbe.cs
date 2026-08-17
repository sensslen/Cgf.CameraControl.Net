using AtemSharp.Communication;
using AtemSharp.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Cgf.CameraControl.Atem;

// AtemSharp discovers its command types via Assembly.GetTypes(). Trimming can empty that registry
// without any error, so a published build has to prove the parser still decodes a real command.
public static class AotProbe
{
    public static async Task<string> Touch()
    {
        var services = new ServiceCollection().AddAtemSharp().BuildServiceProvider().GetRequiredService<IServices>();

        var parser = services.CreateCommandParser();
        var known = Describe(parser, "PrgI", [0, 0, 0, 1]);
        var unknown = Describe(parser, "ZZZZ", [0, 0, 0, 0]);

        await using var client = services.CreateAtemClient();
        var verdict = known.StartsWith("null", StringComparison.Ordinal) || known == unknown
            ? "FAIL: command registry is empty, trimming removed the command types"
            : "OK: command registry survived trimming";

        return $"parser PrgI -> {known}, ZZZZ -> {unknown}\n{verdict}";
    }

    private static string Describe(ICommandParser parser, string name, byte[] payload)
    {
        try
        {
            return parser.ParseCommand(name, payload)?.GetType().Name ?? "null";
        }
        catch (Exception ex)
        {
            return $"{ex.GetType().Name}";
        }
    }
}
