using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Net;

namespace Cgf.CameraControl.Core.Configuration;

/// A device address: an IPv4 or IPv6 literal or a DNS host name. Anything else would surface later
/// as a connection that never comes up. An empty value is left to [Required].
[AttributeUsage(AttributeTargets.Property)]
public sealed class HostAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        value is not string { Length: > 0 } host ||
        IPAddress.TryParse(host, out _) ||
        Uri.CheckHostName(host) != UriHostNameType.Unknown;

    public override string FormatErrorMessage(string name) => $"The field {name} must be a host name or IP address.";
}

/// What a SignalR hub connection accepts. Paths are appended to the raw string, which a query or a
/// fragment would swallow.
[AttributeUsage(AttributeTargets.Property)]
public sealed class HttpUrlAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        value is not string { Length: > 0 } url ||
        (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
         (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
         uri.Query.Length == 0 && uri.Fragment.Length == 0);

    public override string FormatErrorMessage(string name) =>
        $"The field {name} must be an http or https URL without a query or a fragment.";
}

/// [MinLength] needs reflection to count anything that is not an ICollection, so trimming flags it.
[AttributeUsage(AttributeTargets.Property)]
public sealed class NotEmptyAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is not ICollection { Count: 0 };

    public override string FormatErrorMessage(string name) => $"The field {name} must hold at least one entry.";
}

/// [Range] cannot reach the numbers inside a list or a map, and the generator cannot enumerate a map.
[AttributeUsage(AttributeTargets.Property)]
public sealed class NonNegativeNumbersAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value switch
    {
        IEnumerable<KeyValuePair<int, int>> map => map.All(pair => pair is { Key: >= 0, Value: >= 0 }),
        IEnumerable<int> numbers => numbers.All(number => number >= 0),
        _ => true,
    };

    public override string FormatErrorMessage(string name) => $"The field {name} must hold only numbers of 0 or more.";
}
