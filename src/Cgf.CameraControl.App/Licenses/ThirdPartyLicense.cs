namespace Cgf.CameraControl.App.Licenses;

/// One NuGet package this application ships, exactly as the licences window shows it, the text of its
/// licence included. The values are written into the assembly by the licence source generator, so
/// displaying them costs nothing at run time: no resource to find, no JSON to parse, no string to
/// build.
public sealed record ThirdPartyLicense(
    string Title,
    string? License,
    string? Copyright,
    string? Authors,
    string? ProjectUrl,
    string? Text);
