using Cgf.CameraControl.Core.Configuration;

namespace Cgf.CameraControl.Core.GenericFactory;

public interface IBuilder<T, in TConfiguration>
    where TConfiguration : EntryConfiguration
{
    /// Null when the entry is not one this builder makes, so the factory can offer it to the next one.
    Task<T>? BuildAsync(TConfiguration entry, CancellationToken cancellationToken);
}
