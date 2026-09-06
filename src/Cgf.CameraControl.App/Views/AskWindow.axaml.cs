using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;
using Cgf.CameraControl.App.Editing;
using Cgf.CameraControl.App.Localization;

namespace Cgf.CameraControl.App.Views;

/// The one question the editor asks: a sentence, the list it turns on, and the answers that are
/// available. Both of its uses need a list under the question, so it is one window with two callers
/// rather than two windows sharing a shape.
public partial class AskWindow : Window
{
    public AskWindow()
    {
        InitializeComponent();
        DataContext = Localizer.Current;
    }

    /// Save is offered only when nothing blocks it. Cancel is always offered, so a mode entered by
    /// accident is never a choice between losing the work and being unable to leave.
    public static async Task<SaveChoice> LeavingAsync(
        Window owner,
        bool canSave,
        IReadOnlyList<string> issues)
    {
        var window = Build(
            Localizer.Current.Text("edit.leaveTitle"),
            Localizer.Current.Text("edit.leaveQuestion"),
            canSave ? null : Localizer.Current.Text("edit.leaveBlocked"),
            canSave ? [] : issues);

        var answer = SaveChoice.Cancel;
        if (canSave)
        {
            window.Answer(Localizer.Current.Text("edit.save"), () => answer = SaveChoice.Save, primary: true);
        }

        window.Answer(Localizer.Current.Text("edit.discard"), () => answer = SaveChoice.Discard);
        window.Answer(Localizer.Current.Text("edit.cancel"), () => answer = SaveChoice.Cancel);

        await window.ShowDialog(owner).ConfigureAwait(true);
        return answer;
    }

    /// A camera can be taken out of the maps that name it, so those are reported as going away. A
    /// mixer cannot: an interface has to drive one, so those are reported as left to be re-pointed.
    public static async Task<bool> DeletingAsync(Window owner, EntryDraft entry, IReadOnlyList<string> references)
    {
        var window = Build(
            Localizer.Current.Text("edit.deleteTitle"),
            string.Format(
                CultureInfo.CurrentUICulture,
                Localizer.Current.Text("edit.deleteQuestion"),
                entry.Title),
            references.Count == 0
                ? null
                : Localizer.Current.Text(entry.Kind == EntryKind.Camera
                    ? "edit.deleteReferencesCleared"
                    : "edit.deleteReferences"),
            references);

        var confirmed = false;
        window.Answer(Localizer.Current.Text("edit.delete"), () => confirmed = true, primary: true);
        window.Answer(Localizer.Current.Text("edit.cancel"), () => confirmed = false);

        await window.ShowDialog(owner).ConfigureAwait(true);
        return confirmed;
    }

    private static AskWindow Build(string title, string question, string? preamble, IReadOnlyList<string> points)
    {
        var window = new AskWindow { Title = title };
        window.Question.Text = question;
        window.Preamble.Text = preamble;
        window.Points.ItemsSource = points;
        window.Preamble.IsVisible = window.Detail.IsVisible = preamble is not null && points.Count > 0;
        return window;
    }

    private void Answer(string caption, Action chosen, bool primary = false)
    {
        var button = new Button
        {
            Content = caption,
            MinWidth = 96,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            IsDefault = primary,
        };

        button.Click += (_, _) =>
        {
            chosen();
            Close();
        };

        Answers.Children.Add(button);
    }
}
