using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.GenericFactory;
using Cgf.CameraControl.Core.Hmi;
using Cgf.CameraControl.Core.Logger;
using Cgf.CameraControl.Core.VideoMixer;

namespace Cgf.CameraControl.Core;

public sealed class CameraControlCore(ILogger logger) : IAsyncDisposable
{
    public CameraConnectionFactory CameraFactory { get; } = new();

    public VideoMixerFactory MixerFactory { get; } = new();

    public HmiFactory HmiFactory { get; } = new();

    // Cameras before mixers before interfaces: an interface resolves its mixer and its cameras while
    // it is being built.
    public async Task<IReadOnlyList<LoadIssue>> BootstrapAsync(RootConfig config, CancellationToken cancellationToken)
    {
        var issues = new List<LoadIssue>();

        foreach (var entry in config.Cams)
        {
            Collect(issues, await CameraFactory.ParseConfigAsync(entry, logger, cancellationToken).ConfigureAwait(false));
        }

        foreach (var entry in config.VideoMixers)
        {
            Collect(issues, await MixerFactory.ParseConfigAsync(entry, logger, cancellationToken).ConfigureAwait(false));
        }

        foreach (var entry in config.Interfaces)
        {
            Collect(issues, await HmiFactory.ParseConfigAsync(entry, logger, cancellationToken).ConfigureAwait(false));
        }

        return issues;
    }

    // Teardown drops the built instances but keeps the registered builders, so the same core can take
    // a new configuration without the host re-registering everything.
    public async Task<IReadOnlyList<LoadIssue>> ReconfigureAsync(RootConfig config, CancellationToken cancellationToken)
    {
        await DisposeAsync().ConfigureAwait(false);
        return await BootstrapAsync(config, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await HmiFactory.DisposeAsync().ConfigureAwait(false);
        await MixerFactory.DisposeAsync().ConfigureAwait(false);
        await CameraFactory.DisposeAsync().ConfigureAwait(false);
    }

    private static void Collect(List<LoadIssue> issues, LoadIssue? issue)
    {
        if (issue is not null)
        {
            issues.Add(issue);
        }
    }
}
