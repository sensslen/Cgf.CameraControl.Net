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

    /// Raised when a value changes, so the document can revalidate and mark itself dirty.
    public event EventHandler? Edited;

    /// False until the constructor has put the value it read into the property. Setting a property
    /// runs the same handler an operator's typing does, and a field must not report the record as
    /// edited for having been read.
    protected bool Loaded { get; private set; }

    protected internal abstract void Validate();

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
    private readonly bool _required;
    private readonly Action<string?> _write;

    public TextField(
        ILocalizer strings,
        string key,
        string label,
        bool required,
        Func<string?> read,
        Action<string?> write,
        string? placeholder = null,
        IReadOnlyList<string>? suggestions = null)
        : base(strings, key, label, placeholder)
    {
        _required = required;
        _write = write;
        Suggestions = suggestions ?? [];
        Value = read() ?? string.Empty;
        Ready();
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
            ? _required || RequiredWhen?.Invoke() == true ? Strings.Text("edit.required") : null
            : Check?.Invoke(Value);

    partial void OnValueChanged(string value) => Write(() => _write(string.IsNullOrWhiteSpace(value) ? null : value));
}

/// A whole number, backed by its text rather than by an int. A box you can only type a number into is
/// a box you cannot clear, and a half-typed value has to be allowed to exist before it is reported as
/// wrong.
public sealed partial class NumberField : Field
{
    private readonly int _minimum;
    private readonly int _maximum;
    private readonly bool _required;
    private readonly Action<int?> _write;

    public NumberField(
        ILocalizer strings,
        string key,
        string label,
        bool required,
        int minimum,
        Func<int?> read,
        Action<int?> write,
        string? placeholder = null,
        int maximum = int.MaxValue)
        : base(strings, key, label, placeholder)
    {
        _required = required;
        _minimum = minimum;
        _maximum = maximum;
        _write = write;
        Value = read() is { } number ? Format(number) : string.Empty;
        Ready();
    }

    [ObservableProperty]
    public partial string Value { get; set; } = string.Empty;

    public int? Number =>
        int.TryParse(Value, NumberStyles.Integer, CultureInfo.CurrentUICulture, out var parsed) ? parsed : null;

    protected internal override void Validate() =>
        Error = (Value.Length, Number) switch
        {
            (0, _) => _required ? Strings.Text("edit.required") : null,
            (_, null) => Strings.Text("edit.notANumber"),
            (_, var number) when number < _minimum => string.Format(
                CultureInfo.CurrentUICulture,
                Strings.Text("edit.tooSmall"),
                _minimum),
            (_, var number) when number > _maximum => string.Format(
                CultureInfo.CurrentUICulture,
                Strings.Text("edit.outOfRange"),
                _minimum,
                _maximum),
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

    protected internal override void Validate() =>
        Error = Number switch
        {
            null => Strings.Text("edit.notANumber"),
            < 1 or > 100 => string.Format(
                CultureInfo.CurrentUICulture, Strings.Text("edit.outOfRange"), 1, 100),
            _ => null,
        };

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

    protected internal override void Validate()
    {
    }

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

    protected internal override void Validate() =>
        Error = SelectedOption is null ? Strings.Text("edit.required") : null;

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
    private readonly double _maximum;
    private readonly Action<double> _write;

    public FractionField(ILocalizer strings, string key, string label, double maximum, Func<double> read, Action<double> write)
        : base(strings, key, label)
    {
        _maximum = maximum;
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

    protected internal override void Validate() =>
        Error = Number switch
        {
            null => Strings.Text("edit.notANumber"),
            var number when number < 0 || number > _maximum => string.Format(
                CultureInfo.CurrentUICulture,
                Strings.Text("edit.outOfRange"),
                0,
                _maximum),
            _ => null,
        };

    partial void OnValueChanged(string value) => Write(() =>
    {
        if (Number is { } number)
        {
            _write(number);
        }
    });
}
