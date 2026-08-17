using AtemSharp.Communication;
using AtemSharp.DependencyInjection;
using AtemSharp.State;
using Microsoft.Extensions.DependencyInjection;

namespace Cgf.CameraControl.Atem.Tests;

/// AtemSharp's state objects have internal setters, so a test cannot assemble one directly. It can
/// feed real protocol payloads through the library's own parser instead, which drives the state the
/// same way a switcher would and checks the command layouts at the same time.
///
/// The state starts empty and its collections are sized by the switcher's topology, so a driver has
/// to announce a topology before anything can be addressed, exactly as a real connection does.
public sealed class AtemStateDriver
{
    private readonly ICommandParser _parser = new ServiceCollection()
        .AddAtemSharp()
        .BuildServiceProvider()
        .GetRequiredService<IServices>()
        .CreateCommandParser();

    public AtemStateDriver(int mixEffects = 2, int auxiliaries = 6, int keyersPerMixEffect = 4)
    {
        var topology = new byte[32];
        topology[0] = (byte)mixEffects;
        topology[1] = 20;
        topology[2] = 2;
        topology[3] = (byte)auxiliaries;
        Apply("_top", topology);

        for (var me = 0; me < mixEffects; me++)
        {
            Apply("_MeC", (byte)me, (byte)keyersPerMixEffect, 0, 0);
        }
    }

    public AtemState State { get; } = new();

    public void Apply(string command, params byte[] payload)
    {
        var parsed = _parser.ParseCommand(command, payload)
            ?? throw new InvalidOperationException($"AtemSharp has no parser for '{command}'");
        parsed.ApplyToState(State);
    }

    public void ProgramInput(byte mixEffect, ushort source) =>
        Apply("PrgI", mixEffect, 0, (byte)(source >> 8), (byte)source);

    public void PreviewInput(byte mixEffect, ushort source) =>
        Apply("PrvI", mixEffect, 0, (byte)(source >> 8), (byte)source);

    public void KeyerOnAir(byte mixEffect, byte keyer, bool onAir) =>
        Apply("KeOn", mixEffect, keyer, (byte)(onAir ? 1 : 0), 0);

    /// KeBP carries the whole keyer property block; fill source sits at offset 6.
    public void KeyerFillSource(byte mixEffect, byte keyer, ushort source)
    {
        var payload = new byte[32];
        payload[0] = mixEffect;
        payload[1] = keyer;
        payload[6] = (byte)(source >> 8);
        payload[7] = (byte)source;
        Apply("KeBP", payload);
    }

    public void AuxSource(byte aux, ushort source) =>
        Apply("AuxS", aux, 0, (byte)(source >> 8), (byte)source);
}
