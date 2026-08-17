using Cgf.CameraControl.Core.Cameras;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.GenericFactory;
using Cgf.CameraControl.Core.Hmi;
using Cgf.CameraControl.Core.Logging;
using Cgf.CameraControl.Core.VideoMixers;

namespace Cgf.CameraControl.Core;

public sealed class CameraControlCore(ILogger logger) : IAsyncDisposable
{
    public Factory<ICameraConnection> Cameras { get; } = new("cams");

    public Factory<IVideoMixer> VideoMixers { get; } = new("videoMixers");

    public Factory<IHmi> Interfaces { get; } = new("interfaces");

    // Teardown drops the built instances but keeps the registered builders, so the same core can
    // take a new configuration without the host re-registering everything.
    public async Task<IReadOnlyList<LoadIssue>> ReconfigureAsync(RootConfig config, CancellationToken cancellationToken)
    {
        await DisposeAsync().ConfigureAwait(false);
        return await BootstrapAsync(config, cancellationToken).ConfigureAwait(false);
    }

    // Cameras before mixers before interfaces: an interface resolves its mixer and cameras while it builds.
    public async Task<IReadOnlyList<LoadIssue>> BootstrapAsync(RootConfig config, CancellationToken cancellationToken)
    {
        var issues = new List<LoadIssue>();

        foreach (var entry in config.Cams)
        {
            Collect(issues, await Cameras.ParseConfigAsync(entry, logger, cancellationToken).ConfigureAwait(false));
        }

        foreach (var entry in config.VideoMixers)
        {
            Collect(issues, await VideoMixers.ParseConfigAsync(entry, logger, cancellationToken).ConfigureAwait(false));
        }

        foreach (var entry in config.Interfaces)
        {
            Collect(issues, await Interfaces.ParseConfigAsync(entry, logger, cancellationToken).ConfigureAwait(false));
        }

        return issues;
    }

    public async ValueTask DisposeAsync()
    {
        await Interfaces.DisposeAsync().ConfigureAwait(false);
        await VideoMixers.DisposeAsync().ConfigureAwait(false);
        await Cameras.DisposeAsync().ConfigureAwait(false);
    }

    private static void Collect(List<LoadIssue> issues, LoadIssue? issue)
    {
        if (issue is not null)
        {
            issues.Add(issue);
        }
    }
}
