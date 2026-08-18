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
            throw new ConfigValidationException(Locate(ex.Path), Explain(ex.Message));
        }
        catch (NotSupportedException ex)
        {
            // An absent polymorphic discriminator, such as a connectionChange with no type, arrives
            // as NotSupportedException rather than JsonException. Without this the framework's own
            // message escapes naming no entry, and the operator is told a CLR type is unsupported
            // instead of which line of their file is wrong.
            throw new ConfigValidationException(Locate(PathIn(ex.Message)), Explain(ex.Message));
        }
    }

    public override string ToString() => $"{Type}[{Instance}]";

    private string Locate(string? path) =>
        string.IsNullOrEmpty(path) ? ToString() : $"{this}{path.TrimStart('$')}";

    // The message repeats the path and line info the caller already prints.
    private static string Explain(string message)
    {
        var cut = message.IndexOf(" Path: ", StringComparison.Ordinal);
        return cut < 0 ? message : message[..cut];
    }

    // NotSupportedException carries no Path property, only the path appended to its message.
    private static string? PathIn(string message)
    {
        var start = message.IndexOf(" Path: ", StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        start += " Path: ".Length;
        var end = message.IndexOf(" | ", start, StringComparison.Ordinal);
        return end < 0 ? message[start..] : message[start..end];
    }
}
