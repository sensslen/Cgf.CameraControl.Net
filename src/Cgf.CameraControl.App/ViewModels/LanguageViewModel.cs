using Cgf.CameraControl.App.Hosting;
using Cgf.CameraControl.App.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cgf.CameraControl.App.ViewModels;

public sealed partial class LanguageViewModel(Language language) : ViewModelBase
{
    public Language Language => language;

    public string Display => language.NativeName;

    [ObservableProperty]
    public partial bool IsActive { get; set; } = Localizer.Current.Active == language;

    [RelayCommand]
    private void Select()
    {
        Localizer.Current.Use(language);
        Settings.Update(settings => settings.Language = language.Culture);
    }
}
