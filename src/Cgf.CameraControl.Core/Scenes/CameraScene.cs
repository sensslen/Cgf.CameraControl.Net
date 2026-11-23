using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json.Serialization;
using Cgf.CameraControl.Core.CameraConnection;
using Cgf.CameraControl.Core.Configuration;
using Microsoft.Extensions.Options;

namespace Cgf.CameraControl.Core.Scenes;

/// A named set of framings, one per camera instance, each storing only the parts chosen for it.
public sealed record CameraScene(
    [property: JsonRequired, Required] string Name,
    [property: JsonRequired] IReadOnlyDictionary<int, ScenePosition> Cameras)
{
    /// Two names an operator would read as the same scene are the same scene.
    public static bool SameName(string left, string right) =>
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    /// The generator cannot enumerate a map, so each camera is checked here, its key by hand.
    public IEnumerable<ValueProblem> Validate() =>
        ValueProblem.Of(new CameraSceneValidator(), this).Concat(Cameras.SelectMany(pair => Camera(pair.Key, pair.Value)));

    // System.Text.Json does not apply nullable annotations to dictionary values, so null arrives.
    private static IEnumerable<ValueProblem> Camera(int instance, ScenePosition? stored)
    {
        var path = $"cameras.{instance.ToString(CultureInfo.InvariantCulture)}";
        return (instance, stored) switch
        {
            ( <= 0, _) => [new ValueProblem(path, "expected a positive camera instance")],
            (_, null) => [new ValueProblem(path, "expected an object, found null")],
            _ => stored.Validate().Select(problem => problem.Under(path)),
        };
    }
}

/// A stored framing as the file writes it: pan and tilt as two keys, although a camera only ever
/// reports and drives them together. VISCA carries each in 16 bits: pan and tilt signed, zoom and
/// focus unsigned.
public sealed record ScenePosition(
    [property: Range(short.MinValue, short.MaxValue)] int? Pan,
    [property: Range(short.MinValue, short.MaxValue)] int? Tilt,
    [property: Range(0, ushort.MaxValue)] int? Zoom,
    [property: Range(0, ushort.MaxValue)] int? Focus) : IValidatableObject
{
    [JsonIgnore]
    public CameraPosition Position =>
        new(Pan is { } pan && Tilt is { } tilt ? new PanTiltPosition(pan, tilt) : null, Zoom, Focus);

    public static ScenePosition From(CameraPosition position) =>
        new(position.PanTilt?.Pan, position.PanTilt?.Tilt, position.Zoom, position.Focus);

    public IEnumerable<ValueProblem> Validate() => ValueProblem.Of(new ScenePositionValidator(), this);

    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        if (Pan.HasValue != Tilt.HasValue)
        {
            yield return new ValidationResult("'pan' and 'tilt' are stored together");
        }
        else if (Position.IsEmpty)
        {
            yield return new ValidationResult("stores nothing");
        }
    }
}

[OptionsValidator]
internal sealed partial class CameraSceneValidator : IValidateOptions<CameraScene>;

[OptionsValidator]
internal sealed partial class ScenePositionValidator : IValidateOptions<ScenePosition>;
