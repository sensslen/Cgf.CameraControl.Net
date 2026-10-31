using System.Globalization;
using System.Reactive.Linq;
using System.Text.Json;
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

    /// The value as text, whatever JSON type the file wrote it as. A hand-written `"port": "52381"`
    /// is shown for correction rather than taking the form down with it.
    protected static string Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : node?.ToJsonString() ?? string.Empty;

    /// True when the file holds this key as something other than the given JSON kind.
    protected bool Mistyped(params JsonValueKind[] kinds) =>
        Entry[Key] is { } node && !kinds.Contains(node.GetValueKind());

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
        Value = Text(entry[key]);
        Ready();
        if (Mistyped(JsonValueKind.String))
        {
            OnValueChanged(Value);
        }
    }

    public IReadOnlyList<string> Suggestions { get; }

    /// Required only while this holds, such as a serial number while the backend is HID.
    public Func<bool>? RequiredWhen { get; init; }

    /// A rule beyond being present, returning what is wrong or null.
    public Func<string, string?>? Check { get; init; }

    public bool HasSuggestions => Suggestions.Count > 0;

    [ObservableProperty]
    public partial string Value { get; set; } = string.Empty;

    protected internal override void Validate() =>
        Error = string.IsNullOrWhiteSpace(Value)
            ? _required || RequiredWhen?.Invoke() == true ? Localizer.Current.Text("edit.required") : null
            : Check?.Invoke(Value);

    partial void OnValueChanged(string value) =>
        Write(string.IsNullOrWhiteSpace(value) ? null : JsonValue.Create(value));
}

/// A whole number, backed by its text rather than by an int. A box you can only type a number into is
/// a box you cannot clear, and a half-typed value has to be allowed to exist before it is reported as
/// wrong.
public sealed partial class NumberField : Field
{
    private readonly int _minimum;
    private readonly int _maximum;
    private readonly bool _required;

    public NumberField(
        JsonObject entry,
        string key,
        string label,
        bool required,
        int minimum,
        string? placeholder = null,
        int maximum = int.MaxValue)
        : base(entry, key, label, placeholder)
    {
        _required = required;
        _minimum = minimum;
        _maximum = maximum;
        var node = entry[key];
        Value = node is JsonValue stored && stored.TryGetValue<int>(out var number)
            ? number.ToString(CultureInfo.CurrentUICulture)
            : Text(node);
        Ready();
        if (node is not null && !(node is JsonValue written && written.TryGetValue<int>(out _)))
        {
            OnValueChanged(Value);
        }
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
            (_, var number) when number > _maximum => string.Format(
                CultureInfo.CurrentUICulture,
                Localizer.Current.Text("edit.outOfRange"),
                _minimum,
                _maximum),
            _ => null,
        };

    partial void OnValueChanged(string value) => Write(Number is { } number ? JsonValue.Create(number) : null);
}

public sealed partial class PercentField : Field
{
    public PercentField(JsonObject entry, string key, string label)
        : base(entry, key, label)
    {
        var node = entry[key];
        Value = node is null ? "100"
            : node is JsonValue stored && stored.TryGetValue<int>(out var number) ? number.ToString(CultureInfo.CurrentUICulture)
            : Text(node);
        Ready();
        if (node is not null && !(node is JsonValue written && written.TryGetValue<int>(out _)))
        {
            OnValueChanged(Value);
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SliderValue))]
    public partial string Value { get; set; } = string.Empty;

    private int? Number =>
        int.TryParse(Value, NumberStyles.Integer, CultureInfo.CurrentUICulture, out var parsed) ? parsed : null;

    public double SliderValue
    {
        get => Math.Clamp(Number ?? 100, 1, 100);
        set => Value = Math.Round(Math.Clamp(value, 1, 100)).ToString(CultureInfo.CurrentUICulture);
    }

    protected internal override void Validate() =>
        Error = Number switch
        {
            null => Localizer.Current.Text("edit.notANumber"),
            < 1 or > 100 => string.Format(
                CultureInfo.CurrentUICulture, Localizer.Current.Text("edit.outOfRange"), 1, 100),
            _ => null,
        };

    partial void OnValueChanged(string value) => Write(Number is { } number ? JsonValue.Create(number) : null);
}

public sealed partial class SwitchField : Field
{
    public SwitchField(JsonObject entry, string key, string label, bool fallback)
        : base(entry, key, label)
    {
        var mistyped = Mistyped(JsonValueKind.True, JsonValueKind.False);
        Value = entry[key] switch
        {
            null => fallback,
            var node when !mistyped => node.GetValue<bool>(),
            var node => bool.TryParse(Text(node), out var parsed) ? parsed : fallback,
        };
        Ready();
        if (mistyped)
        {
            OnValueChanged(Value);
        }
    }

    [ObservableProperty]
    public partial bool Value { get; set; }

    protected internal override void Validate()
    {
    }

    partial void OnValueChanged(bool value) => Write(JsonValue.Create(value));
}

public sealed record ChoiceOption(string Value, string? CaptionKey)
{
    public IObservable<string> Caption =>
        CaptionKey is { } key ? Localizer.Current[key] : Observable.Return(Value);
}

/// One of a fixed set of names, which is how every enum in the configuration is written.
public sealed partial class ChoiceField : Field
{
    public ChoiceField(
        JsonObject entry,
        string key,
        string label,
        IReadOnlyList<string> options,
        string fallback,
        string? optionLabelPrefix = null)
        : base(entry, key, label)
    {
        Options = options;
        Items = options.Select(value => new ChoiceOption(
            value, optionLabelPrefix is null ? null : $"{optionLabelPrefix}.{value}")).ToArray();
        var written = entry.TryGetPropertyValue(key, out var node) ? node?.ToString() ?? string.Empty : fallback;
        Value = options.FirstOrDefault(option => string.Equals(option, written, StringComparison.OrdinalIgnoreCase))
            ?? written;
        Ready();
    }

    public IReadOnlyList<string> Options { get; }

    public IReadOnlyList<ChoiceOption> Items { get; }

    public ChoiceOption? SelectedOption
    {
        get => Items.FirstOrDefault(option => string.Equals(option.Value, Value, StringComparison.OrdinalIgnoreCase));
        set
        {
            if (value is not null)
            {
                Value = value.Value;
            }
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedOption))]
    public partial string Value { get; set; } = string.Empty;

    protected internal override void Validate() =>
        Error = SelectedOption is null ? Localizer.Current.Text("edit.required") : null;

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
        var node = entry[key];
        Value = node is null ? fallback.ToString(CultureInfo.CurrentUICulture)
            : node is JsonValue stored && stored.TryGetValue<double>(out var number) ? number.ToString(CultureInfo.CurrentUICulture)
            : Text(node);
        Ready();
        if (Mistyped(JsonValueKind.Number))
        {
            OnValueChanged(Value);
        }
    }

    [ObservableProperty]
    public partial string Value { get; set; } = string.Empty;

    private double? Number =>
        double.TryParse(Value, NumberStyles.Float, CultureInfo.CurrentUICulture, out var parsed) && double.IsFinite(parsed)
            ? parsed
            : null;

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
