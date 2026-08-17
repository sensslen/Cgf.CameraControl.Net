using Cgf.CameraControl.Core.Configuration;

namespace Cgf.CameraControl.Core.GenericFactory;

public interface IBuilder<T>
{
    IReadOnlyCollection<string> SupportedTypes { get; }

    Task<T> BuildAsync(ConfigEntry entry, CancellationToken cancellationToken);
}
