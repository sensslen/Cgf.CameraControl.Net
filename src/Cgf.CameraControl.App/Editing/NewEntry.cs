using Cgf.CameraControl.App.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cgf.CameraControl.App.Editing;

/// An entry being added, which exists nowhere until it is accepted. The type comes first because it
/// decides which fields there are to fill, and changing it starts the entry again rather than carrying
/// values across to a shape that has no room for them.
public sealed partial class NewEntry : ViewModelBase
{
    private readonly EntryKind _kind;
    private readonly int _instance;
    private readonly IReadOnlyList<string> _pads;

    public NewEntry(EntryKind kind, int instance, IReadOnlyList<string>? pads = null)
    {
        _kind = kind;
        _instance = instance;
        _pads = pads ?? [];
        Types = kind switch
        {
            EntryKind.Camera => EntrySchema.CameraTypes,
            EntryKind.Mixer => EntrySchema.MixerTypes,
            _ => EntrySchema.InterfaceTypes,
        };

        SelectedType = Types[0];
        Entry = Build();
    }

    public IReadOnlyList<string> Types { get; }

    [ObservableProperty]
    public partial string SelectedType { get; set; } = string.Empty;

    [ObservableProperty]
    public partial EntryDraft Entry { get; set; } = null!;

    public bool CanAdd => Entry.Fields.All(entry => entry.Error is null);

    partial void OnSelectedTypeChanged(string value)
    {
        Entry = Build();
        OnPropertyChanged(nameof(CanAdd));
    }

    private EntryDraft Build()
    {
        var entry = new EntryDraft(_kind, SelectedType, EntrySchema.NewEntry(SelectedType, _instance), _pads);
        entry.Edited += (_, _) => OnPropertyChanged(nameof(CanAdd));
        return entry;
    }
}
