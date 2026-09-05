using Avalonia.Controls;
using Cgf.CameraControl.App.Localization;

namespace Cgf.CameraControl.App.Views;

public partial class LicensesWindow : Window
{
    public LicensesWindow()
    {
        InitializeComponent();
        DataContext = Localizer.Current;
    }
}
