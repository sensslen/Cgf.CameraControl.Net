using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json.Nodes;
using Cgf.CameraControl.App.Localization;
using Cgf.CameraControl.App.ViewModels;
using Cgf.CameraControl.Core.Configuration;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cgf.CameraControl.App.Editing;

/// The configuration while it is being edited, and every rule that decides whether it may be saved.
///
/// Only what this can be sure of blocks a save: a name the file uses that the file does not define. A
/// direction selecting a mixer input with no camera behind it is not one of them, because a mixer
/// input without a camera on it is an ordinary desk.
public sealed partial class ConfigDraft : ViewModelBase
{
    public ObservableCollection<EntryDraft> Cameras { get; } = [];

    public ObservableCollection<EntryDraft> Mixers { get; } = [];

    public ObservableCollection<EntryDraft> Interfaces { get; } = [];

    /// What stops a save, in the order the collections are drawn in.
    public ObservableCollection<string> Issues { get; } = [];

    public bool CanSave => Issues.Count == 0;

    [ObservableProperty]
    public partial bool IsDirty { get; set; }

    /// The pads SDL reports right now, offered where a configuration names one. Nothing consumes them
    /// while the mode is on, which is what makes them available to offer.
    public IReadOnlyList<string> Pads { get; private set; } = [];

    public static ConfigDraft From(RootConfig config, IReadOnlyList<string>? pads = null)
    {
        var draft = new ConfigDraft { Pads = pads ?? [] };
        draft.Fill(EntryKind.Camera, draft.Cameras, config.Cams);
        draft.Fill(EntryKind.Mixer, draft.Mixers, config.VideoMixers);
        draft.Fill(EntryKind.Interface, draft.Interfaces, config.Interfaces);
        draft.Validate();
        return draft;
    }

    public RootConfig ToConfig() =>
        new(
            [.. Cameras.Select(entry => entry.ToEntry())],
            [.. Mixers.Select(entry => entry.ToEntry())],
            [.. Interfaces.Select(entry => entry.ToEntry())]);

    /// The lowest number nothing in that collection has taken, so a new entry is proposed one that
    /// does not collide rather than one that has to be corrected before it can be saved.
    public int NextInstance(EntryKind kind)
    {
        var list = For(kind);
        return Enumerable.Range(1, list.Count + 1).First(number => list.All(entry => entry.Instance != number));
    }

    public EntryDraft Add(EntryKind kind, string type)
    {
        var draft = new EntryDraft(kind, type, EntrySchema.NewEntry(type, NextInstance(kind)), Pads);
        Insert(draft);
        return draft;
    }

    public void Insert(EntryDraft draft)
    {
        For(draft.Kind).Add(Attach(draft));
        IsDirty = true;
        Validate();
    }

    /// What would be left naming this entry once it is gone, so the operator is told before it is,
    /// rather than finding it in the issue list afterwards.
    public IReadOnlyList<string> ReferencesTo(EntryDraft entry)
    {
        if (entry.Instance is not { } instance)
        {
            return [];
        }

        return entry.Kind switch
        {
            EntryKind.Camera =>
                [.. Interfaces.Where(other => other.MappedCameras().Contains(instance)).Select(other => other.Title)],
            EntryKind.Mixer =>
                [.. Interfaces.Where(other => Mixer(other) == instance).Select(other => other.Title)],
            _ => [],
        };
    }

    /// A camera can be taken out of the maps that named it. A mixer cannot: an interface has to drive
    /// one, so what is left is an issue that says to point it somewhere else.
    public void Remove(EntryDraft entry)
    {
        if (entry.Kind == EntryKind.Camera && entry.Instance is { } instance)
        {
            foreach (var other in Interfaces)
            {
                other.DropCamera(instance);
            }
        }

        For(entry.Kind).Remove(entry);
        IsDirty = true;
        Validate();
    }

    private ObservableCollection<EntryDraft> For(EntryKind kind) => kind switch
    {
        EntryKind.Camera => Cameras,
        EntryKind.Mixer => Mixers,
        _ => Interfaces,
    };

    private static int? Mixer(EntryDraft entry) =>
        entry.Fields.OfType<NumberField>().FirstOrDefault(field => field.Key == "videoMixer")?.Number;

    private void Fill(EntryKind kind, ObservableCollection<EntryDraft> list, IReadOnlyList<ConfigEntry> entries)
    {
        foreach (var entry in entries)
        {
            var node = JsonNode.Parse(entry.Raw.GetRawText()) as JsonObject ?? [];
            list.Add(Attach(new EntryDraft(kind, entry.Type, node, Pads)));
        }
    }

    private EntryDraft Attach(EntryDraft draft)
    {
        draft.Edited += (_, _) =>
        {
            IsDirty = true;
            Validate();
        };

        return draft;
    }

    private void Validate()
    {
        Issues.Clear();

        Report(Cameras, "panel.cameras");
        Report(Mixers, "panel.mixers");
        Report(Interfaces, "panel.interfaces");

        var cameras = Cameras.Select(entry => entry.Instance).OfType<int>().ToHashSet();
        var mixers = Mixers.Select(entry => entry.Instance).OfType<int>().ToHashSet();

        foreach (var entry in Interfaces)
        {
            foreach (var camera in entry.MappedCameras().Distinct().Where(camera => !cameras.Contains(camera)))
            {
                Flag(entry, "edit.issue.noCamera", camera);
            }

            if (Mixer(entry) is { } mixer && !mixers.Contains(mixer))
            {
                Flag(entry, "edit.issue.noMixer", mixer);
            }

            var defined = entry.DefinedFunctionNames().ToHashSet(StringComparer.Ordinal);
            foreach (var name in entry.BoundFunctionNames().Distinct(StringComparer.Ordinal)
                         .Where(name => !defined.Contains(name)))
            {
                Flag(entry, "edit.issue.noFunction", name);
            }
        }

        OnPropertyChanged(nameof(CanSave));
    }

    private void Report(IReadOnlyList<EntryDraft> list, string section)
    {
        foreach (var entry in list)
        {
            entry.Problem = null;
        }

        foreach (var entry in list)
        {
            foreach (var field in entry.Fields.Where(field => field.Error is not null))
            {
                Note(entry, string.Format(
                    CultureInfo.CurrentUICulture,
                    Localizer.Current.Text("edit.issue.field"),
                    entry.Title,
                    Localizer.Current.Text(field.Label),
                    field.Error));
            }
        }

        foreach (var group in list.Where(entry => entry.Instance is not null)
                     .GroupBy(entry => entry.Instance)
                     .Where(group => group.Count() > 1))
        {
            foreach (var entry in group)
            {
                Note(entry, string.Format(
                    CultureInfo.CurrentUICulture,
                    Localizer.Current.Text("edit.issue.repeatedInstance"),
                    Localizer.Current.Text(section),
                    group.Key));
            }
        }
    }

    private void Flag(EntryDraft entry, string key, object argument) =>
        Note(entry, string.Format(
            CultureInfo.CurrentUICulture,
            Localizer.Current.Text(key),
            entry.Title,
            argument));

    private void Note(EntryDraft entry, string message)
    {
        entry.Problem ??= message;
        Issues.Add(message);
    }
}
