using Avalonia.Media;

namespace Cgf.CameraControl.App.Localization;

/// The strings of the language in use, handed to whatever shows text by the composition root. XAML
/// reaches it through a view model's Strings property, bound as `Strings[key]^`.
public interface ILocalizer
{
    Language Active { get; }

    IObservable<FlowDirection> FlowDirection { get; }

    /// A missing key shows as its own name rather than as blank, so a gap in a translation is
    /// visible instead of leaving an unlabelled button.
    IObservable<string> this[string key] { get; }

    event EventHandler? LanguageChanged;

    /// The string as it reads now, for the few places that cannot take an observable: a native menu
    /// item's header, and the application's own name.
    string Text(string key);

    void Use(Language language);
}
