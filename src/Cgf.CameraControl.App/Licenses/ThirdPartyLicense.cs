namespace Cgf.CameraControl.App.Licenses;

/// One NuGet package this application ships, exactly as the licences window shows it. The values are
/// written into the assembly by the licence source generator, so displaying them costs nothing at
/// run time: no resource to find, no JSON to parse, no string to build.
public sealed record ThirdPartyLicense(
    string Title,
    string? License,
    string? Copyright,
    string? Authors,
    string? ProjectUrl);

/// The full text of one licence, shown once rather than once per package, because the packages
/// naming it are all under the same words.
public sealed record ThirdPartyLicenseText(string Name, string Text);
