using System.Text.Json;

namespace Cgf.CameraControl.Core.Configuration;

// Instance and Type are the only fields the core understands. Everything else stays as raw JSON
// until the builder that claims Type deserializes it into its own shape, mirroring how each
// TypeScript builder ran its own zod schema over the entry.
public sealed record ConfigEntry(int Instance, string Type, JsonElement Raw)
{
    public T? Deserialize<T>(JsonSerializerOptions options) => Raw.Deserialize<T>(options);

    public override string ToString() => $"instance {Instance} of type '{Type}'";
}
