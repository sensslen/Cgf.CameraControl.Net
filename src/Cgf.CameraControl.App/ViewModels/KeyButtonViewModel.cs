using System.Windows.Input;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cgf.CameraControl.App.ViewModels;

/// One button on the keyboard and mouse panel. It is a control to click and a picture of a key at
/// the same time: clicking it runs the binding, and holding the key it names lights it up, so the
/// panel says what the configuration file says without the operator reading the file.
public sealed partial class KeyButtonViewModel(string label, Key? key, ICommand command) : ViewModelBase
{
    /// The function's configured name, or the input's number. Never invented here: an operator who
    /// named a function badly should see their own name and go and fix it.
    public string Label { get; } = label;

    /// Null when nothing is bound, which is what leaves the corner of the button empty rather than
    /// claiming a shortcut that does not exist.
    public Key? Key { get; } = key;

    /// Empty rather than absent when nothing is bound, so every button keeps the same shape.
    public string KeyText { get; } = key?.ToString() ?? string.Empty;

    public ICommand Command { get; } = command;

    [ObservableProperty]
    public partial bool IsHeld { get; set; }

    /// Only an input button carries these. Preview and program are the two colours a video desk is
    /// read in, and putting them on the button removes the need for a second place to look.
    [ObservableProperty]
    public partial bool IsPreview { get; set; }

    [ObservableProperty]
    public partial bool IsProgram { get; set; }
}
