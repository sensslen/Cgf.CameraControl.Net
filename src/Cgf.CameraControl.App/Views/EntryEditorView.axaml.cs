using Avalonia.Controls;

namespace Cgf.CameraControl.App.Views;

/// The form for one configuration entry. The fields it draws come from the entry's type, and each one
/// writes straight into the JSON that entry will be saved as.
public partial class EntryEditorView : UserControl
{
    public EntryEditorView() => InitializeComponent();
}
