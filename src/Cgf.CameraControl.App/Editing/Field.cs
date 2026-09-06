using System.Globalization;
using System.Text.Json.Nodes;
using Cgf.CameraControl.App.Localization;
using Cgf.CameraControl.App.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cgf.CameraControl.App.Editing;

/// One property of one configuration entry, bound to the JSON that entry carries.
///
/// A field reads its value out of that JSON when it is built and writes back only when it is changed
/// afterwards, which is what lets an entry somebody merely looked at come out of the writer exactly as
/// it went in. Everything no field covers, the functions and the bindings among them, rides along
/// inside the same object untouched.
public abstract partial class Field(JsonObject entry, string key, string label, string? placeholder = null)
    : ViewModelBase
{
    protected JsonObject Entry { get; } = entry;

    public string Key { get; } = key;

    /// A localization key, so the form follows the language menu like everything else.
    public string Label { get; } = label;

    /// What an empty box would mean, shown inside it. A field that is optional says what happens when
    /// it is left alone rather than looking like one somebody forgot.
    public string? Placeholder { get; } = placeholder;

    public IObservable<string> Caption => Localizer.Current[Label];

    public IObservable<string> Hint => Localizer.Current[Placeholder ?? string.Empty];

    /// What stops a save, in the words the operator will read. Null while the field is fine.
    [ObservableProperty]
    public partial string? Error { get; set; }

    /// Raised when a value changes, so the document can revalidate and mark itself dirty.
    public event EventHandler? Edited;

    /// False until the constructor has put the value it read into the property. Setting a property
    /// runs the same handler an operator's typing does, and a field must not report the file as
    /// edited for having been read.
    protected bool Loaded { get; private set; }

    protected internal abstract void Validate();

    protected void Ready()
    {
        Validate();
        Loaded = true;
    }

    protected void Write(JsonNode? value)
    {
        if (!Loaded)
        {
            return;
        }

        if (value is null)
        {
            Entry.Remove(Key);
        }
        else
        {
            Entry[Key] = value;
        }

        Validate();
        Edited?.Invoke(this, EventArgs.Empty);
    }
}

/// A free string: an address, a serial number, a serial port.
///
/// Suggestions turn it into a picker that still takes anything typed. A pad's serial is one of the
/// pads plugged in right now, until it is a desk being built away from them.
public sealed partial class TextField : Field
{
    private readonly bool _required;

    public TextField(
        JsonObject entry,
        string key,
        string label,
        bool required,
        string? placeholder = null,
        IReadOnlyList<string>? suggestions = null)
        : base(entry, key, label, placeholder)
    {
        _required = required;
        Suggestions = suggestions ?? [];
        Value = entry[key]?.GetValue<string>() ?? string.Empty;
        Ready();
    }

    public IReadOnlyList<string> Suggestions { get; }

    public bool HasSuggestions => Suggestions.Count > 0;

    [ObservableProperty]
    public partial string Value { get; set; } = string.Empty;

    protected internal override void Validate() =>
        Error = _required && string.IsNullOrWhiteSpace(Value) ? Localizer.Current.Text("edit.required") : null;

    partial void OnValueChanged(string value) =>
        Write(string.IsNullOrWhiteSpace(value) ? null : JsonValue.Create(value));
}

/// A whole number, backed by its text rather than by an int. A box you can only type a number into is
/// a box you cannot clear, and a half-typed value has to be allowed to exist before it is reported as
/// wrong.
public sealed partial class NumberField : Field
{
    private readonly int _minimum;
    private readonly bool _required;

    public NumberField(
        JsonObject entry,
        string key,
        string label,
        bool required,
        int minimum,
        string? placeholder = null)
        : base(entry, key, label, placeholder)
    {
        _required = required;
        _minimum = minimum;
        Value = entry[key] is { } node ? node.GetValue<int>().ToString(CultureInfo.CurrentUICulture) : string.Empty;
        Ready();
    }

    [ObservableProperty]
    public partial string Value { get; set; } = string.Empty;

    public int? Number =>
        int.TryParse(Value, NumberStyles.Integer, CultureInfo.CurrentUICulture, out var parsed) ? parsed : null;

    protected internal override void Validate() =>
        Error = (Value.Length, Number) switch
        {
            (0, _) => _required ? Localizer.Current.Text("edit.required") : null,
            (_, null) => Localizer.Current.Text("edit.notANumber"),
            (_, var number) when number < _minimum => string.Format(
                CultureInfo.CurrentUICulture,
                Localizer.Current.Text("edit.tooSmall"),
                _minimum),
            _ => null,
        };

    partial void OnValueChanged(string value) => Write(Number is { } number ? JsonValue.Create(number) : null);
}

public sealed partial class SwitchField : Field
{
    public SwitchField(JsonObject entry, string key, string label, bool fallback)
        : base(entry, key, label)
    {
        Value = entry[key]?.GetValue<bool>() ?? fallback;
        Ready();
    }

    [ObservableProperty]
    public partial bool Value { get; set; }

    protected internal override void Validate()
    {
    }

    partial void OnValueChanged(bool value) => Write(JsonValue.Create(value));
}

/// One of a fixed set of names, which is how every enum in the configuration is written.
public sealed partial class ChoiceField : Field
{
    public ChoiceField(JsonObject entry, string key, string label, IReadOnlyList<string> options, string fallback)
        : base(entry, key, label)
    {
        Options = options;
        var written = entry[key]?.GetValue<string>();
        Value = written is not null && options.Contains(written, StringComparer.OrdinalIgnoreCase)
            ? written
            : fallback;
        Ready();
    }

    public IReadOnlyList<string> Options { get; }

    [ObservableProperty]
    public partial string Value { get; set; } = string.Empty;

    protected internal override void Validate()
    {
    }

    partial void OnValueChanged(string value) => Write(JsonValue.Create(value));
}

/// A number with a decimal point, for the one field that takes a fraction.
public sealed partial class FractionField : Field
{
    private readonly double _maximum;

    public FractionField(JsonObject entry, string key, string label, double fallback, double maximum)
        : base(entry, key, label)
    {
        _maximum = maximum;
        Value = (entry[key]?.GetValue<double>() ?? fallback).ToString(CultureInfo.CurrentUICulture);
        Ready();
    }

    [ObservableProperty]
    public partial string Value { get; set; } = string.Empty;

    private double? Number =>
        double.TryParse(Value, NumberStyles.Float, CultureInfo.CurrentUICulture, out var parsed) ? parsed : null;

    protected internal override void Validate() =>
        Error = Number switch
        {
            null => Localizer.Current.Text("edit.notANumber"),
            var number when number < 0 || number > _maximum => string.Format(
                CultureInfo.CurrentUICulture,
                Localizer.Current.Text("edit.outOfRange"),
                0,
                _maximum),
            _ => null,
        };

    partial void OnValueChanged(string value) => Write(Number is { } number ? JsonValue.Create(number) : null);
}
