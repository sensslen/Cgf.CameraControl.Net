namespace Cgf.CameraControl.Core.Scenes;

/// The scenes the desk recalls from. Replaced as a whole when a scene is saved, so an interface
/// reading it at the moment a button is pressed sees either the old set or the new one.
public sealed class SceneLibrary
{
    private IReadOnlyList<CameraScene> _scenes = [];

    public IReadOnlyList<CameraScene> Scenes => Volatile.Read(ref _scenes);

    public void Replace(IReadOnlyList<CameraScene> scenes) => Volatile.Write(ref _scenes, scenes);

    public CameraScene? Find(string name) =>
        Scenes.FirstOrDefault(scene => CameraScene.SameName(scene.Name, name));
}
