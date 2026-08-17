using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Cgf.CameraControl.Core.Configuration;

/// The TypeScript `configSchema` is a loose object carrying only instance and type. Everything else
/// stays as raw JSON until the builder that claims the type deserializes it into its own shape,
/// which is how each builder ran its own zod schema over the entry.
public sealed record ConfigEntry(int Instance, string Type, JsonElement Raw)
{
    public T Deserialize<T>(JsonTypeInfo<T> typeInfo)
    {
        try
        {
            return Raw.Deserialize(typeInfo) ?? throw new ConfigValidationException(ToString(), "is empty");
        }
        catch (JsonException ex)
        {
            throw new ConfigValidationException(Locate(ex), Explain(ex));
        }
    }

    public override string ToString() => $"{Type}[{Instance}]";

    private string Locate(JsonException ex) =>
        string.IsNullOrEmpty(ex.Path) ? ToString() : $"{this}{ex.Path.TrimStart('$')}";

    // The message repeats the path and line info the caller already prints.
    private static string Explain(JsonException ex)
    {
        var cut = ex.Message.IndexOf(" Path: ", StringComparison.Ordinal);
        return cut < 0 ? ex.Message : ex.Message[..cut];
    }
}
