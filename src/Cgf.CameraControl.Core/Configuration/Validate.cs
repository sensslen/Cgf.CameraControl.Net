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
