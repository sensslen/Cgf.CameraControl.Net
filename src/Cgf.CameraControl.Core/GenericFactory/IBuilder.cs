using Cgf.CameraControl.Core.Configuration;

namespace Cgf.CameraControl.Core.GenericFactory;

/// Builds the entries of one section whose type it claims. The entry arrives typed as its section,
/// and the builder narrows it to the record registered for the type it claims.
public interface IBuilder<T, in TConfiguration>
    where TConfiguration : EntryConfiguration
{
    IReadOnlyCollection<string> SupportedTypes { get; }

    Task<T> BuildAsync(TConfiguration entry, CancellationToken cancellationToken);
}
