using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Cgf.CameraControl.Core.Configuration;

/// A value that a record's own rules refuse, at the path the file spells the property by.
public sealed record ValueProblem(string Path, string Message)
{
    public static IEnumerable<ValueProblem> Of<T>(IValidateOptions<T> validator, T value)
        where T : class =>
        validator.Validate(null, value).Failures?.Select(Parse) ?? [];

    public ValueProblem Under(string parent) => this with { Path = Path.Length == 0 ? parent : $"{parent}.{Path}" };

    // A generated validator reports each failure only as text, "Member: message", and the message
    // names the member by its C# path below the record validated, such as "Condition.Key".
    private static ValueProblem Parse(string failure)
    {
        var colon = failure.IndexOf(": ", StringComparison.Ordinal);
        if (colon < 0)
        {
            return new ValueProblem(string.Empty, failure);
        }

        var member = failure[..colon];
        var message = failure[(colon + 2)..];
        var named = message.Split(' ').FirstOrDefault(word => word == member || word.EndsWith($".{member}", StringComparison.Ordinal))
                    ?? member;
        var path = string.Join('.', named.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
        return new ValueProblem(path, message.Replace(named, path, StringComparison.Ordinal));
    }
}
