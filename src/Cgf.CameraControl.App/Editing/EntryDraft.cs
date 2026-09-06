using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cgf.CameraControl.App.ViewModels;
using Cgf.CameraControl.Core.Configuration;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cgf.CameraControl.App.Editing;

public enum EntryKind
{
    Camera,
    Mixer,
    Interface,
}

/// One entry of the configuration while it is being edited: the JSON it will be written back as, and
/// the fields that reach into it.
///
/// The type is fixed once the entry exists. Changing it would replace every field on the form and
/// leave the values that belonged to the old shape with nowhere to go, so a type that turns out to be
/// wrong is deleted and added again.
public sealed partial class EntryDraft : ViewModelBase
{
    private readonly JsonObject _entry;

    public EntryDraft(EntryKind kind, string type, JsonObject entry)
    {
        Kind = kind;
        Type = type;
        _entry = entry;

        InstanceField = new NumberField(entry, "instance", "edit.instance", required: true, minimum: 1);
        Fields = [InstanceField, .. EntrySchema.Fields(type, entry)];

        foreach (var field in Fields)
        {
            field.Edited += OnFieldEdited;
        }

        Title = Describe();
    }

    public EntryKind Kind { get; }

    public string Type { get; }

    public NumberField InstanceField { get; }

    public IReadOnlyList<Field> Fields { get; }

    /// Null while the box is empty or holds something that is not a number, which is exactly when the
    /// entry cannot be told apart from another one.
    public int? Instance => InstanceField.Number;

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    /// Set by the document when a rule the entry alone cannot see is broken: a camera it names that
    /// is not configured, an instance another entry already claims.
    [ObservableProperty]
    public partial string? Problem { get; set; }

    public bool HasFields => Fields.Count > 1;

    public event EventHandler? Edited;

    public ConfigEntry ToEntry() =>
        new(Instance ?? 0, Type, JsonDocument.Parse(_entry.ToJsonString()).RootElement.Clone());

    /// The names this entry binds to functions, whichever kind of interface it is. Read out of the
    /// JSON rather than off a field, because the bindings themselves are edited on the drawing.
    public IEnumerable<string> BoundFunctionNames()
    {
        if (_entry["pad"] is JsonObject pad)
        {
            foreach (var name in pad.SelectMany(set => Values(set.Value)))
            {
                yield return name;
            }
        }

        if (_entry["keys"]?["function"] is JsonObject functions)
        {
            foreach (var name in Values(functions))
            {
                yield return name;
            }
        }
    }

    public IEnumerable<string> DefinedFunctionNames() =>
        _entry["functions"] is JsonObject functions ? functions.Select(pair => pair.Key) : [];

    /// The cameras this entry's map names, so the document can say which of them are not configured.
    public IEnumerable<int> MappedCameras() =>
        Fields.OfType<MapField>().SelectMany(field => field.Targets);

    public void DropCamera(int instance)
    {
        foreach (var field in Fields.OfType<MapField>())
        {
            foreach (var row in field.Rows
                         .Where(row => row.Value == instance.ToString(CultureInfo.CurrentUICulture))
                         .ToList())
            {
                field.RemoveRowCommand.Execute(row);
            }
        }
    }

    private static IEnumerable<string> Values(JsonNode? node) =>
        node is JsonObject map
            ? map.Select(pair => pair.Value?.GetValue<string>()).OfType<string>()
            : [];

    private string Describe() =>
        Instance is { } instance
            ? string.Format(CultureInfo.CurrentUICulture, "{0} [{1}]", Type, instance)
            : Type;

    private void OnFieldEdited(object? sender, EventArgs e)
    {
        Title = Describe();
        Edited?.Invoke(this, EventArgs.Empty);
    }
}
