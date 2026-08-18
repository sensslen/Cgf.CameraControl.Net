using System.Windows.Input;
using Avalonia.Controls;
using Cgf.CameraControl.App.Localization;
using Cgf.CameraControl.App.ViewModels;

namespace Cgf.CameraControl.App.Views;

/// macOS puts an application's menu in the system menu bar, not inside the window, and a menu drawn
/// in the window is the loudest sign that an application is not really a Mac application. Avalonia's
/// NativeMenu maps to the real menu bar there, so the same commands are published twice: through the
/// window's own Menu on Windows and Linux, and through this on macOS.
///
/// Built in code rather than XAML because NativeMenu has no ItemsSource and the language list is
/// generated, and rebuilt outright on a language change because the headers are plain strings.
public static class SystemMenu
{
    public static void Attach(Window window, MainViewModel model)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        Build(window, model);
        Localizer.Current.LanguageChanged += (_, _) => Build(window, model);
    }

    private static void Build(Window window, MainViewModel model)
    {
        var file = new NativeMenuItem(Text("menu.file")) { Menu = [] };
        file.Menu.Add(Command(Text("config.import"), model.ImportCommand));
        file.Menu.Add(Command(Text("config.export"), model.ExportCommand));
        file.Menu.Add(Command(Text("config.reload"), model.ReloadCommand));

        var languages = new NativeMenuItem(Text("language.label")) { Menu = [] };
        foreach (var language in model.Languages)
        {
            var item = Command(language.Display, language.SelectCommand);
            item.ToggleType = MenuItemToggleType.Radio;
            item.IsChecked = language.IsActive;
            languages.Menu.Add(item);
        }

        NativeMenu.SetMenu(window, [file, languages]);
    }

    private static NativeMenuItem Command(string header, ICommand command) =>
        new(header) { Command = command };

    private static string Text(string key)
    {
        var text = key;
        using (Localizer.Current[key].Subscribe(value => text = value))
        {
            return text;
        }
    }
}
