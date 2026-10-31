namespace Cgf.CameraControl.Core.Configuration;

/// System.Text.Json checks shape, not domain, so the zod refinements that constrain a value rather
/// than its type are applied here after deserialization.
public static class Validate
{
    /// z.int().nonnegative()
    public static int NonNegative(int value, string path) =>
        value >= 0 ? value : throw new ConfigValidationException(path, $"expected a non-negative integer, found {value}");

    /// z.int().positive()
    public static int Positive(int value, string path) =>
        value > 0 ? value : throw new ConfigValidationException(path, $"expected a positive integer, found {value}");

    /// z.number().gt(minimumExclusive).max(maximum)
    public static double InRange(double value, double minimumExclusive, double maximum, string path) =>
        value > minimumExclusive && value <= maximum
            ? value
            : throw new ConfigValidationException(path, $"expected a number in ]{minimumExclusive} .. {maximum}], found {value}");

    /// What a socket can be bound to.
    public static int Port(int value, string path) =>
        value is > 0 and <= 65535
            ? value
            : throw new ConfigValidationException(path, $"expected a port in 1 .. 65535, found {value}");

    /// What a SignalR hub connection accepts.
    public static string HttpUrl(string value, string path) =>
        IsHttpUrl(value) ? value : throw new ConfigValidationException(path, $"expected an http or https URL, found {value}");

    /// SignalR appends its negotiate path to the raw string, which a query or fragment would swallow.
    public static bool IsHttpUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
        uri.Query.Length == 0 && uri.Fragment.Length == 0;

    /// z.array(...).min(count)
    public static IReadOnlyList<T> MinCount<T>(IReadOnlyList<T>? items, int count, string path) =>
        items is not null && items.Count >= count
            ? items
            : throw new ConfigValidationException(path, $"expected at least {count} entries, found {items?.Count ?? 0}");

    /// z.record(z.string().regex(/^\d+$/), z.int().nonnegative())
    public static IReadOnlyDictionary<int, int> NonNegativeMap(IReadOnlyDictionary<int, int>? map, string path)
    {
        if (map is null)
        {
            throw new ConfigValidationException(path, "is required");
        }

        foreach (var (key, value) in map)
        {
            NonNegative(key, $"{path}.{key}");
            NonNegative(value, $"{path}.{key}");
        }

        return map;
    }
}
