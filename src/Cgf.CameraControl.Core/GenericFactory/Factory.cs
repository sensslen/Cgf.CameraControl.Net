using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.Logger;

namespace Cgf.CameraControl.Core.GenericFactory;

public abstract class Factory<T, TConfiguration>(string section) : IAsyncDisposable
    where T : IAsyncDisposable
    where TConfiguration : EntryConfiguration
{
    private readonly Dictionary<string, IBuilder<T, TConfiguration>> _builders = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, T> _instances = [];

    // Every instance arrives through ParseConfigAsync, so whenever there is something to dispose
    // there is a logger to report its failure to.
    private ILogger? _logger;

    public IReadOnlyDictionary<int, T> Instances => _instances;

    public T? Get(int instance) => _instances.TryGetValue(instance, out var value) ? value : default;

    /// The type strings the registered builders claim.
    public IReadOnlyCollection<string> ClaimedTypes => _builders.Keys;

    public void AddBuilder(IBuilder<T, TConfiguration> builder)
    {
        foreach (var type in builder.SupportedTypes)
        {
            if (!_builders.TryAdd(type, builder))
            {
                throw new InvalidOperationException(
                    $"{section}: type '{type}' is already claimed by {_builders[type].GetType().Name}");
            }
        }
    }

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

        if (!_builders.TryGetValue(entry.Type, out var builder))
        {
            return Issue(entry, $"no builder claims type '{entry.Type}'. Known types: {string.Join(", ", _builders.Keys.Order())}");
        }

        try
        {
            _instances[entry.Instance] = await builder.BuildAsync(entry, cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Issue(entry, ex.Message);
        }

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
