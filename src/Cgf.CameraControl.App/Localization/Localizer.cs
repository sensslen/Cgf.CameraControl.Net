using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Media;

namespace Cgf.CameraControl.App.Localization;

public sealed record Language(string Culture, string NativeName);

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = false)]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class LanguageJson : JsonSerializerContext;

/// The strings are embedded manifest resources rather than resx satellites, because NativeAOT does
/// not load satellite assemblies: a resx build would compile, publish, and then silently show
/// English. The aot-probe switch checks a non-English string still resolves in the published binary.
///
/// Bound from XAML through the indexer, so switching language re-reads every string in place.
public sealed class Localizer : INotifyPropertyChanged
{
    public const string SourceLanguage = "en";

    private static readonly Assembly Owner = typeof(Localizer).Assembly;

    private static readonly Dictionary<string, string> Fallback = Read(SourceLanguage);

    private Dictionary<string, string> _strings = Fallback;

    private Localizer()
    {
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// The ten most written languages, in that order, plus German. English is the source; every
    /// other language is machine translated and none has been reviewed by a native speaker.
    public static IReadOnlyList<Language> Languages { get; } =
    [
        new("en", "English"),
        new("zh-Hans", "简体中文"),
        new("hi", "हिन्दी"),
        new("es", "Español"),
        new("ar", "العربية"),
        new("fr", "Français"),
        new("bn", "বাংলা"),
        new("pt", "Português"),
        new("ru", "Русский"),
        new("ur", "اردو"),
        new("de", "Deutsch"),
    ];

    /// Declared after Languages because static initialisers run in order and the instance this
    /// creates reads that list.
    public static Localizer Current { get; } = new();

    public Language Active { get; private set; } = Languages[0];

    public FlowDirection FlowDirection =>
        Active.Culture is "ar" or "ur" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    /// A missing key shows as its own name rather than as blank, so a gap in a translation is
    /// visible instead of leaving an unlabelled button.
    public string this[string key] =>
        _strings.TryGetValue(key, out var text) ? text : Fallback.GetValueOrDefault(key, key);

    /// The closest language to the system's, falling back through the parent culture so a machine
    /// set to pt-BR gets Portuguese rather than English.
    public static Language Match(CultureInfo culture)
    {
        for (var candidate = culture; !string.IsNullOrEmpty(candidate.Name); candidate = candidate.Parent)
        {
            if (Languages.FirstOrDefault(language => Equal(language.Culture, candidate.Name)) is { } matched)
            {
                return matched;
            }

            // zh-CN and zh-SG write simplified Chinese but neither name contains "Hans".
            if (Equal(candidate.Name, "zh-CN") || Equal(candidate.Name, "zh-SG"))
            {
                return Languages.First(language => language.Culture == "zh-Hans");
            }
        }

        return Languages[0];
    }

    public void Use(Language language)
    {
        if (language == Active)
        {
            return;
        }

        Active = language;
        _strings = Read(language.Culture);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Active)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FlowDirection)));
    }

    private static bool Equal(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, string> Read(string culture)
    {
        using var stream = Owner.GetManifestResourceStream($"Cgf.CameraControl.App.Localization.Strings.{culture}.json");
        return stream is null
            ? []
            : JsonSerializer.Deserialize(stream, LanguageJson.Default.DictionaryStringString) ?? [];
    }
}
