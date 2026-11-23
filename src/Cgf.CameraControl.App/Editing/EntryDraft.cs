using System.Globalization;
using Cgf.CameraControl.App.Localization;
using Cgf.CameraControl.App.ViewModels;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cgf.CameraControl.App.Editing;

public enum EntryKind
{
    Camera,
    Mixer,
    Interface,
}

/// One entry of the configuration while it is being edited: a copy of its record, and the fields that
/// read and write it.
///
/// The type is fixed once the entry exists. Changing it would replace every field on the form and
/// leave the values that belonged to the old shape with nowhere to go, so a type that turns out to be
/// wrong is deleted and added again.
///
/// An entry the loader could not read has no fields. It is shown with what is wrong with it and
/// written back as it was read, so it can be deleted, or corrected in the file, but is never lost.
public abstract partial class EntryDraft : ViewModelBase
{
    private readonly EntryConfiguration _entry;

    private protected EntryDraft(ILocalizer strings, EntryKind kind, EntryConfiguration entry, IReadOnlyList<string>? pads)
    {
        Strings = strings;
        Kind = kind;
        _entry = entry;

        if (_entry is IInvalidConfiguration)
        {
            Fields = [];
        }
        else
        {
            InstanceField = new NumberField(strings, "instance", "edit.instance", required: true, minimum: 1,
                () => _entry.Instance, value => _entry.Instance = value ?? 0);
            Fields = [InstanceField, .. EntrySchema.Fields(strings, _entry, pads ?? [])];
        }

        foreach (var field in Fields)
        {
            field.Validate();
            field.Edited += OnFieldEdited;
        }

        Title = Describe();
    }

    public ILocalizer Strings { get; }

    public EntryKind Kind { get; }

    public string Type => _entry.Type;

    /// Null for an entry the loader could not read, whose instance cannot be changed here.
    public NumberField? InstanceField { get; }

    public IReadOnlyList<Field> Fields { get; }

    /// Null while the box is empty or holds something that is not a number, which is exactly when the
    /// entry cannot be told apart from another one. An entry the loader could not read keeps the
    /// number it was written with, so what refers to it, and what would collide with it once it is
    /// corrected, is still seen.
    public int? Instance => InstanceField is { } instance
        ? instance.Number
        : _entry.Instance != 0 ? _entry.Instance : (int?)null;

    /// Why the loader could not read this entry, or null when it could.
    public string? Unreadable => _entry is IInvalidConfiguration invalid ? invalid.Problem : null;

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    /// Set by the document when a rule the entry alone cannot see is broken: a camera it names that
    /// is not configured, an instance another entry already claims.
    [ObservableProperty]
    public partial string? Problem { get; set; }

    public bool HasFields => Fields.Count > 1;

    public event EventHandler? Edited;

    public static EntryDraft<CameraConfiguration> Camera(ILocalizer strings, CameraConfiguration entry, IReadOnlyList<string>? pads = null) =>
        new(strings, EntryKind.Camera, entry with { }, record => record with { }, pads);

    public static EntryDraft<VideoMixerConfiguration> Mixer(ILocalizer strings, VideoMixerConfiguration entry, IReadOnlyList<string>? pads = null) =>
        new(strings, EntryKind.Mixer, entry with { }, record => record with { }, pads);

    public static EntryDraft<InterfaceConfiguration> Interface(ILocalizer strings, InterfaceConfiguration entry, IReadOnlyList<string>? pads = null) =>
        new(strings, EntryKind.Interface, entry with { }, record => record with { }, pads);

    /// A copy, so an edit made after the configuration is taken does not reach into it.
    public abstract EntryConfiguration ToEntry();

    /// The names this entry binds to functions, whichever kind of interface it is. Read off the
    /// record rather than off a field, because the bindings themselves are edited on the drawing.
    public IEnumerable<string> BoundFunctionNames() => _entry switch
    {
        GamepadConfiguration pad => (pad.Pad?.Default?.Values ?? [])
            .Concat(pad.Pad?.Alt?.Values ?? [])
            .Concat(pad.Pad?.AltLower?.Values ?? []),
        KeyboardConfiguration keyboard => keyboard.Keys.Function?.Values ?? [],
        _ => [],
    };

    public IEnumerable<string> DefinedFunctionNames() =>
        _entry is ControlInterfaceConfiguration { Functions: { } functions } ? functions.Keys : [];

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

    private string Describe()
    {
        var name = Type.Length == 0 ? "?" : Type;
        return Instance is { } instance
            ? string.Format(CultureInfo.CurrentUICulture, "{0} [{1}]", name, instance)
            : name;
    }

    private void OnFieldEdited(object? sender, EventArgs e)
    {
        // A field's rule can depend on another field, as the serial number does on the backend.
        foreach (var field in Fields.Where(field => field != sender))
        {
            field.Validate();
        }

        Title = Describe();
        Edited?.Invoke(this, EventArgs.Empty);
    }
}

/// The copy is passed in because a `with` expression cannot be applied to a type parameter.
public sealed class EntryDraft<TSection> : EntryDraft
    where TSection : EntryConfiguration
{
    private readonly TSection _entry;
    private readonly Func<TSection, TSection> _copy;

    internal EntryDraft(ILocalizer strings, EntryKind kind, TSection entry, Func<TSection, TSection> copy, IReadOnlyList<string>? pads)
        : base(strings, kind, entry, pads)
    {
        _entry = entry;
        _copy = copy;
    }

    public override TSection ToEntry() => _copy(_entry);
}
