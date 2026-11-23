using Cgf.CameraControl.Core.Scenes;

namespace Cgf.CameraControl.Core.Tests;

public class SceneLibraryTests
{
    [Fact]
    public void TheLibraryFindsASceneRegardlessOfCaseAndSpaces()
    {
        var library = new SceneLibrary();
        library.Replace([new CameraScene("Sermon", new Dictionary<int, ScenePosition>())]);

        Assert.NotNull(library.Find(" sermon"));
        Assert.Null(library.Find("wide"));
    }
}
