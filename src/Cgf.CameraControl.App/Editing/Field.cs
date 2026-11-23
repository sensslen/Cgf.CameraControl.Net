using System.Globalization;
using System.Reactive.Linq;
using Cgf.CameraControl.App.Localization;
using Cgf.CameraControl.App.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cgf.CameraControl.App.Editing;

/// One property of one configuration record, read and written through the accessors it is given.
///
/// A field reads its value from the record when it is built and writes back only when it is changed
/// afterwards. Everything no field covers, the functions and the bindings among them, stays in the
/// record untouched.
public abstract partial class Field(ILocalizer strings, string key, string label, string? placeholder = null)
    : ViewModelBase
{
    public ILocalizer Strings { get; } = strings;

    /// The property's name in the file, which is also how a field is found.
    public string Key { get; } = key;

    /// A localization key, so the form follows the language menu like everything else.
    public string Label { get; } = label;

    /// What an empty box would mean, shown inside it. A field that is optional says what happens when
    /// it is left alone rather than looking like one somebody forgot.
    public string? Placeholder { get; } = placeholder;

    public IObservable<string> Caption => Strings[Label];

    public IObservable<string> Hint => Strings[Placeholder ?? string.Empty];

    /// What stops a save, in the words the operator will read. Null while the field is fine.
    [ObservableProperty]
    public partial string? Error { get; set; }

    /// What the record's own rules say about the value this field wrote, set by the entry.
    public string? Refusal { get; private set; }

    private string? TextError { get; set; }

    /// Raised when a value changes, so the document can revalidate and mark itself dirty.
    public event EventHandler? Edited;

    /// False until the constructor has put the value it read into the property. Setting a property
    /// runs the same handler an operator's typing does, and a field must not report the record as
    /// edited for having been read.
    protected bool Loaded { get; private set; }

    /// Text that the property's type cannot hold never reaches the record, so no rule on the
    /// record can see it.
    protected abstract string? CheckText();

    protected internal void Validate()
    {
        TextError = CheckText();
        Error = TextError ?? Refusal;
    }

    internal void Refuse(string? refusal)
    {
        Refusal = refusal;
        Error = TextError ?? Refusal;
    }

    protected void Ready()
    {
        Validate();
        Loaded = true;
    }

    /// Applies an operator's change to the record. The record follows the box whenever the box holds
    /// a value of the right kind, even one a rule refuses: what is refused is reported, and blocks the
    /// save, rather than leaving the record saying something the form no longer shows.
    protected void Write(Action apply)
    {
        if (!Loaded)
        {
            return;
        }

        apply();
        Validate();
        Edited?.Invoke(this, EventArgs.Empty);
    }

    protected static string Format(int value) => value.ToString(CultureInfo.CurrentUICulture);
}

/// A free string: an address, a serial number, a serial port.
///
/// Suggestions turn it into a picker that still takes anything typed. A pad's serial is one of the
/// pads plugged in right now, until it is a desk being built away from them.
public sealed partial class TextField : Field
{
    private readonly Action<string?> _write;

    public TextField(
        ILocalizer strings,
        string key,
        string label,
        Func<string?> read,
        Action<string?> write,
        string? placeholder = null,
        IReadOnlyList<string>? suggestions = null)
        : base(strings, key, label, placeholder)
    {
        _write = write;
        Suggestions = suggestions ?? [];
        Value = read() ?? string.Empty;
        Ready();
    }

    public IReadOnlyList<string> Suggestions { get; }

    public bool HasSuggestions => Suggestions.Count > 0;

    [ObservableProperty]
    public partial string Value { get; set; } = string.Empty;

    protected override string? CheckText() => null;

    partial void OnValueChanged(string value) => Write(() => _write(string.IsNullOrWhiteSpace(value) ? null : value));
}

/// A whole number, backed by its text rather than by an int. A box you can only type a number into is
/// a box you cannot clear, and a half-typed value has to be allowed to exist before it is reported as
/// wrong.
public sealed partial class NumberField : Field
{
    private readonly bool _required;
    private readonly Action<int?> _write;

    public NumberField(
        ILocalizer strings,
        string key,
        string label,
        bool required,
        Func<int?> read,
        Action<int?> write,
        string? placeholder = null)
        : base(strings, key, label, placeholder)
    {
        _required = required;
        _write = write;
        Value = read() is { } number ? Format(number) : string.Empty;
        Ready();
    }

    [ObservableProperty]
    public partial string Value { get; set; } = string.Empty;

    public int? Number =>
        int.TryParse(Value, NumberStyles.Integer, CultureInfo.CurrentUICulture, out var parsed) ? parsed : null;

    /// Empty is not a number, so a property that cannot be absent cannot hold it.
    protected override string? CheckText() =>
        (Value.Length, Number) switch
        {
            (0, _) => _required ? Strings.Text("edit.required") : null,
            (_, null) => Strings.Text("edit.notANumber"),
            _ => null,
        };

    partial void OnValueChanged(string value) => Write(() => _write(Number));
}

public sealed partial class PercentField : Field
{
    private readonly Action<int> _write;

    public PercentField(ILocalizer strings, string key, string label, Func<int> read, Action<int> write)
        : base(strings, key, label)
    {
        _write = write;
        Value = Format(read());
        Ready();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SliderValue))]
    public partial string Value { get; set; } = string.Empty;

    private int? Number =>
        int.TryParse(Value, NumberStyles.Integer, CultureInfo.CurrentUICulture, out var parsed) ? parsed : null;

    public double SliderValue
    {
        get => Math.Clamp(Number ?? 100, 1, 100);
        set => Value = Format((int)Math.Round(Math.Clamp(value, 1, 100)));
    }

    protected override string? CheckText() => Number is null ? Strings.Text("edit.notANumber") : null;

    partial void OnValueChanged(string value) => Write(() =>
    {
        if (Number is { } number)
        {
            _write(number);
        }
    });
}

public sealed partial class SwitchField : Field
{
    private readonly Action<bool> _write;

    public SwitchField(ILocalizer strings, string key, string label, Func<bool> read, Action<bool> write)
        : base(strings, key, label)
    {
        _write = write;
        Value = read();
        Ready();
    }

    [ObservableProperty]
    public partial bool Value { get; set; }

    protected override string? CheckText() => null;

    partial void OnValueChanged(bool value) => Write(() => _write(value));
}

public sealed record ChoiceOption(ILocalizer Strings, string Value, string? CaptionKey)
{
    public IObservable<string> Caption =>
        CaptionKey is { } key ? Strings[key] : Observable.Return(Value);
}

/// One of a fixed set of names, which is how every enum in the configuration is written.
public sealed partial class ChoiceField : Field
{
    private readonly Action<string> _write;

    public ChoiceField(
        ILocalizer strings,
        string key,
        string label,
        IReadOnlyList<string> options,
        Func<string> read,
        Action<string> write,
        string? optionLabelPrefix = null)
        : base(strings, key, label)
    {
        _write = write;
        Options = options;
        Items = options.Select(value => new ChoiceOption(
            strings,
            value, optionLabelPrefix is null ? null : $"{optionLabelPrefix}.{value}")).ToArray();
        Value = read();
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

    protected override string? CheckText() => SelectedOption is null ? Strings.Text("edit.required") : null;

    partial void OnValueChanged(string value) => Write(() =>
    {
        if (SelectedOption is { } option)
        {
            _write(option.Value);
        }
    });
}

/// A number with a decimal point, for the one field that takes a fraction.
public sealed partial class FractionField : Field
{
    private readonly Action<double> _write;

    public FractionField(ILocalizer strings, string key, string label, Func<double> read, Action<double> write)
        : base(strings, key, label)
    {
        _write = write;
        Value = read().ToString(CultureInfo.CurrentUICulture);
        Ready();
    }

    [ObservableProperty]
    public partial string Value { get; set; } = string.Empty;

    private double? Number =>
        double.TryParse(Value, NumberStyles.Float, CultureInfo.CurrentUICulture, out var parsed) && double.IsFinite(parsed)
            ? parsed
            : null;

    protected override string? CheckText() => Number is null ? Strings.Text("edit.notANumber") : null;

    partial void OnValueChanged(string value) => Write(() =>
    {
        if (Number is { } number)
        {
            _write(number);
        }
    });
}
