using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.Scenes;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared.ConnectionChange;

namespace Cgf.CameraControl.App.Scenes;

/// The interface functions that recall a scene by name.
public static class SceneReferences
{
    /// The functions that name the scene, as "interface: function" for a confirmation to list.
    public static IReadOnlyList<string> To(RootConfig config, string scene) =>
        [.. config.Interfaces.OfType<ControlInterfaceConfiguration>()
            .SelectMany(entry => Recalling(entry, scene).Select(function => $"{entry}: {function}"))];

    /// The configuration with every function that recalls `from` recalling `to` instead, and how
    /// many functions changed.
    public static (RootConfig Config, int Changed) Rename(RootConfig config, string from, string to)
    {
        var changed = 0;
        var interfaces = new List<InterfaceConfiguration>(config.Interfaces.Count);
        foreach (var entry in config.Interfaces)
        {
            if (entry is not ControlInterfaceConfiguration { Functions: { } functions } control
                || !Recalling(control, from).Any())
            {
                interfaces.Add(entry);
                continue;
            }

            var renamed = new Dictionary<string, SpecialFunctionConfiguration>(functions.Count);
            foreach (var (name, function) in functions)
            {
                if (function is SceneSpecialFunctionConfiguration recall && CameraScene.SameName(recall.Name, from))
                {
                    renamed[name] = recall with { Name = to };
                    changed++;
                }
                else
                {
                    renamed[name] = function;
                }
            }

            interfaces.Add(control with { Functions = renamed });
        }

        return (config with { Interfaces = interfaces }, changed);
    }

    private static IEnumerable<string> Recalling(ControlInterfaceConfiguration entry, string scene) =>
        entry.Functions?
            .Where(function => function.Value is SceneSpecialFunctionConfiguration recall
                               && CameraScene.SameName(recall.Name, scene))
            .Select(function => function.Key)
        ?? [];
}
