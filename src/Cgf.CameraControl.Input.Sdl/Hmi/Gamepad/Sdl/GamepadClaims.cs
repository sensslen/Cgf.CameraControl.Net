namespace Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Sdl;

/// Decides which connected pad each configured interface drives.
public static class GamepadClaims
{
    /// Returns, per claim, the index of the pad it takes, or null when it stays unbound. Claims and
    /// pads are given as their configured and reported serial numbers, both of which may be absent.
    ///
    /// A configured serial is an absolute filter, never a preference: an operator whose pad is
    /// unplugged must end up with nothing rather than with a colleague's desk under their sticks.
    /// For the same reason the named claims are served first, so an auto-detecting interface cannot
    /// take a pad that a named one is waiting for.
    public static IReadOnlyList<int?> Assign(IReadOnlyList<string?> claims, IReadOnlyList<string?> pads)
    {
        var assignment = new int?[claims.Count];
        var taken = new bool[pads.Count];

        foreach (var claim in Order(claims))
        {
            for (var pad = 0; pad < pads.Count; pad++)
            {
                if (taken[pad] || !Matches(claims[claim], pads[pad]))
                {
                    continue;
                }

                taken[pad] = true;
                assignment[claim] = pad;
                break;
            }
        }

        return assignment;
    }

    /// Serials are printed on a sticker and typed into a configuration file, so case and separators
    /// are not worth a failed binding.
    public static bool Matches(string? configured, string? reported) =>
        configured is null || string.Equals(Canonical(configured), Canonical(reported), StringComparison.Ordinal);

    private static IEnumerable<int> Order(IReadOnlyList<string?> claims) =>
        Enumerable.Range(0, claims.Count).Where(i => claims[i] is not null)
            .Concat(Enumerable.Range(0, claims.Count).Where(i => claims[i] is null));

    private static string Canonical(string? serial) =>
        serial is null ? string.Empty : string.Concat(serial.Where(char.IsLetterOrDigit)).ToUpperInvariant();
}
