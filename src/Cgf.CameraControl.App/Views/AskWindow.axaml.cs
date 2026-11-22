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
    }

    /// Save is offered only when nothing blocks it. Cancel is always offered, so a mode entered by
    /// accident is never a choice between losing the work and being unable to leave.
    public static async Task<SaveChoice> LeavingAsync(
        Window owner,
        ILocalizer strings,
        bool canSave,
        IReadOnlyList<string> issues)
    {
        var window = Build(
            strings,
            strings.Text("edit.leaveTitle"),
            strings.Text("edit.leaveQuestion"),
            canSave ? null : strings.Text("edit.leaveBlocked"),
            canSave ? [] : issues);

        var answer = SaveChoice.Cancel;
        if (canSave)
        {
            window.Answer(strings.Text("edit.save"), () => answer = SaveChoice.Save, primary: true);
        }

        window.Answer(strings.Text("edit.discard"), () => answer = SaveChoice.Discard);
        window.Answer(strings.Text("edit.cancel"), () => answer = SaveChoice.Cancel);

        await window.ShowDialog(owner).ConfigureAwait(true);
        return answer;
    }

    /// A camera can be taken out of the maps that name it, so those are reported as going away. A
    /// mixer cannot: an interface has to drive one, so those are reported as left to be re-pointed.
    public static async Task<bool> DeletingAsync(
        Window owner,
        ILocalizer strings,
        EntryDraft entry,
        IReadOnlyList<string> references)
    {
        var window = Build(
            strings,
            strings.Text("edit.deleteTitle"),
            string.Format(
                CultureInfo.CurrentUICulture,
                strings.Text("edit.deleteQuestion"),
                entry.Title),
            references.Count == 0
                ? null
                : strings.Text(entry.Kind == EntryKind.Camera
                    ? "edit.deleteReferencesCleared"
                    : "edit.deleteReferences"),
            references);

        var confirmed = false;
        window.Answer(strings.Text("edit.delete"), () => confirmed = true, primary: true);
        window.Answer(strings.Text("edit.cancel"), () => confirmed = false);

        await window.ShowDialog(owner).ConfigureAwait(true);
        return confirmed;
    }

    private static AskWindow Build(
        ILocalizer strings,
        string title,
        string question,
        string? preamble,
        IReadOnlyList<string> points)
    {
        var window = new AskWindow { Title = title, DataContext = strings };
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
