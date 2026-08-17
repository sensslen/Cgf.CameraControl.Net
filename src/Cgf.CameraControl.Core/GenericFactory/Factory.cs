using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.Logger;

namespace Cgf.CameraControl.Core.GenericFactory;

public abstract class Factory<T>(string section) : IAsyncDisposable
    where T : IAsyncDisposable
{
    private readonly Dictionary<string, IBuilder<T>> _builders = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, T> _instances = [];

    public IReadOnlyDictionary<int, T> Instances => _instances;

    public T? Get(int instance) => _instances.TryGetValue(instance, out var value) ? value : default;

    public void AddBuilder(IBuilder<T> builder)
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

    public async Task<LoadIssue?> ParseConfigAsync(ConfigEntry entry, ILogger logger, CancellationToken cancellationToken)
    {
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

        LoadIssue Issue(ConfigEntry failed, string message)
        {
            var issue = new LoadIssue(section, failed, message);
            logger.Error(issue.ToString());
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
            catch
            {
                // Teardown is best effort, exactly as the TypeScript factory treated it.
            }
        }

        _instances.Clear();
    }
}
