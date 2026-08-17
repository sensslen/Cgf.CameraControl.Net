namespace Cgf.CameraControl.App;

// AtemSharp finds its command types through Assembly.GetTypes(), which trimming can empty without
// raising anything, leaving a build that connects to a switcher and decodes nothing. The release
// workflow runs this against every published binary so that failure never reaches a tag.
public static class AotProbes
{
    public static int Run()
    {
        var atem = Atem.AotProbe.Touch().GetAwaiter().GetResult();
        Console.WriteLine(atem.Report);
        Console.WriteLine(Input.Sdl.AotProbe.Touch());
        return atem.RegistryAlive ? 0 : 1;
    }
}
