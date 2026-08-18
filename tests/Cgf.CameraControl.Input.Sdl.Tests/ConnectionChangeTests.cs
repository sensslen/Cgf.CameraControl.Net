using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.ConnectionChange;

namespace Cgf.CameraControl.Input.Sdl.Tests;

public class DirectConnectionChangeTests
{
    private static readonly AltKeyConfiguration Alt = new(Alt: true, AltLower: false);
    private static readonly AltKeyConfiguration AltLower = new(Alt: false, AltLower: true);

    [Fact]
    public void UnmodifiedPressesUseTheDefaultSet()
    {
        Assert.Equal(2, Build().Next(ButtonDirection.Right, 0, AltKeyConfiguration.None));
    }

    [Fact]
    public void AltAndAltLowerEachSelectTheirOwnSet()
    {
        var change = Build();

        Assert.Equal(7, change.Next(ButtonDirection.Right, 0, Alt));
        Assert.Equal(11, change.Next(ButtonDirection.Right, 0, AltLower));
    }

    [Fact]
    public void AModifierWithNoSetConfiguredFallsBackToTheDefault()
    {
        var change = new DirectConnectionChange(new DirectConnectionChangeConfiguration
        {
            Default = new Dictionary<ButtonDirection, int> { [ButtonDirection.Up] = 1 },
        });

        Assert.Equal(1, change.Next(ButtonDirection.Up, 0, Alt));
    }

    // Holding a modifier that has its own set means only that set applies, so an unbound direction
    // does nothing rather than quietly cutting to whatever the unmodified press would have chosen.
    [Fact]
    public void AModifierSetThatOmitsTheDirectionDoesNotFallBack()
    {
        Assert.Null(Build().Next(ButtonDirection.Down, 0, Alt));
    }

    [Fact]
    public void AnUnboundDirectionSelectsNothing()
    {
        Assert.Null(Build().Next(ButtonDirection.Left, 0, AltKeyConfiguration.None));
    }

    private static DirectConnectionChange Build() => new(new DirectConnectionChangeConfiguration
    {
        Default = new Dictionary<ButtonDirection, int> { [ButtonDirection.Up] = 1, [ButtonDirection.Right] = 2 },
        Alt = new Dictionary<ButtonDirection, int> { [ButtonDirection.Right] = 7 },
        AltLower = new Dictionary<ButtonDirection, int> { [ButtonDirection.Right] = 11 },
    });
}

public class DirectionalConnectionChangeTests
{
    [Fact]
    public void StepsFromTheCurrentSelection()
    {
        Assert.Equal(3, Build().Next(ButtonDirection.Down, 1, AltKeyConfiguration.None));
    }

    // JavaScript enumerates integer-like keys in ascending order, so the original fallback of "the
    // first key" means the lowest one, which Dictionary would not reproduce on its own.
    [Fact]
    public void AnUnknownSelectionFallsBackToTheLowestConfiguredInput()
    {
        Assert.Equal(2, Build().Next(ButtonDirection.Right, 99, AltKeyConfiguration.None));
    }

    [Fact]
    public void AnUnboundDirectionSelectsNothing()
    {
        Assert.Null(Build().Next(ButtonDirection.Up, 1, AltKeyConfiguration.None));
    }

    [Fact]
    public void ModifiersDoNotApply()
    {
        var change = Build();

        Assert.Equal(
            change.Next(ButtonDirection.Down, 1, AltKeyConfiguration.None),
            change.Next(ButtonDirection.Down, 1, new AltKeyConfiguration(Alt: true, AltLower: false)));
    }

    private static DirectionalConnectionChange Build() => new(new DirectionalConnectionChangeConfiguration
    {
        Directions = new Dictionary<int, IReadOnlyDictionary<ButtonDirection, int>>
        {
            [2] = new Dictionary<ButtonDirection, int> { [ButtonDirection.Left] = 1 },
            [1] = new Dictionary<ButtonDirection, int> { [ButtonDirection.Right] = 2, [ButtonDirection.Down] = 3 },
        },
    });
}
