using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.Logger;

namespace Cgf.CameraControl.Core.GenericFactory;

public abstract class Factory<T, TConfiguration>(string section) : IAsyncDisposable
    where T : IAsyncDisposable
    where TConfiguration : EntryConfiguration
{
    private readonly List<IBuilder<T, TConfiguration>> _builders = [];
    private readonly Dictionary<int, T> _instances = [];

    // Every instance arrives through ParseConfigAsync, so whenever there is something to dispose
    // there is a logger to report its failure to.
    private ILogger? _logger;

    public IReadOnlyDictionary<int, T> Instances => _instances;

    public T? Get(int instance) => _instances.TryGetValue(instance, out var value) ? value : default;

    public void AddBuilder(IBuilder<T, TConfiguration> builder) => _builders.Add(builder);

    public async Task<LoadIssue?> ParseConfigAsync(TConfiguration entry, ILogger logger, CancellationToken cancellationToken)
    {
        _logger = logger;

        // The loader has reported it already, and there is nothing in it to build from.
        if (entry is IInvalidConfiguration)
        {
            return null;
        }

        if (_instances.ContainsKey(entry.Instance))
        {
            return Issue(entry, $"instance {entry.Instance} is already configured");
        }

        try
        {
            foreach (var builder in _builders)
            {
                if (builder.BuildAsync(entry, cancellationToken) is { } building)
                {
                    _instances[entry.Instance] = await building.ConfigureAwait(false);
                    return null;
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Issue(entry, ex.Message);
        }

        return Issue(entry, $"no builder claims type '{entry.Type}'");

        LoadIssue Issue(TConfiguration failed, string message)
        {
            var issue = new LoadIssue(section, failed, message);
            logger.Error("Configuration", issue.ToString());
            return issue;
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var instance in _instances.Values)
        {
            try
            {
                await instance.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Teardown is best effort: one failure must not keep the rest from being released.
                _logger?.Error(section, $"disposing {instance.GetType().Name} failed - {ex.Message}");
            }
        }

        _instances.Clear();
    }
}
