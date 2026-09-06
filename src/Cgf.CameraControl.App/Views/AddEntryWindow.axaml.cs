using Avalonia.Controls;
using Cgf.CameraControl.App.Editing;

namespace Cgf.CameraControl.App.Views;

/// The add wizard: the type, then the fields that type carries. What it builds exists only here until
/// it is accepted, so an add that is cancelled leaves the configuration exactly as it was.
public partial class AddEntryWindow : Window
{
    private EntryDraft? _accepted;

    public AddEntryWindow()
    {
        InitializeComponent();
        Accept.Click += (_, _) =>
        {
            _accepted = (DataContext as NewEntry)?.Entry;
            Close();
        };

        Reject.Click += (_, _) => Close();
    }

    public static async Task<EntryDraft?> AskAsync(Window owner, NewEntry entry)
    {
        var window = new AddEntryWindow { DataContext = entry };
        await window.ShowDialog(owner).ConfigureAwait(true);
        return window._accepted;
    }
}
