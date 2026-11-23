using System.Collections.ObjectModel;
using System.Globalization;
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
    private readonly ILocalizer _strings;
    private readonly RootConfig _source;

    private ConfigDraft(ILocalizer strings, RootConfig source, IReadOnlyList<string> pads)
    {
        _strings = strings;
        _source = source;
        Pads = pads;
    }

    public ObservableCollection<EntryDraft<CameraConfiguration>> Cameras { get; } = [];

    public ObservableCollection<EntryDraft<VideoMixerConfiguration>> Mixers { get; } = [];

    public ObservableCollection<EntryDraft<InterfaceConfiguration>> Interfaces { get; } = [];

    /// What stops a save, in the order the collections are drawn in.
    public ObservableCollection<string> Issues { get; } = [];

    public bool CanSave => Issues.Count == 0;

    [ObservableProperty]
    public partial bool IsDirty { get; set; }

    /// The pads the backends report right now, offered where a configuration names one. Nothing consumes them
    /// while the mode is on, which is what makes them available to offer.
    public IReadOnlyList<string> Pads { get; }

    public static ConfigDraft From(ILocalizer strings, RootConfig config, IReadOnlyList<string>? pads = null)
    {
        var draft = new ConfigDraft(strings, config, pads ?? []);
        draft.Fill(draft.Cameras, config.Cams, entry => EntryDraft.Camera(strings, entry, draft.Pads));
        draft.Fill(draft.Mixers, config.VideoMixers, entry => EntryDraft.Mixer(strings, entry, draft.Pads));
        draft.Fill(draft.Interfaces, config.Interfaces, entry => EntryDraft.Interface(strings, entry, draft.Pads));
        draft.Validate();
        return draft;
    }

    /// Scenes are not edited here, so they go back exactly as they were loaded.
    public RootConfig ToConfig() =>
        _source with
        {
            Cams = [.. Cameras.Select(entry => entry.ToEntry())],
            VideoMixers = [.. Mixers.Select(entry => entry.ToEntry())],
            Interfaces = [.. Interfaces.Select(entry => entry.ToEntry())],
        };

    /// The lowest number nothing in that collection has taken, so a new entry is proposed one that
    /// does not collide rather than one that has to be corrected before it can be saved.
    public int NextInstance(EntryKind kind)
    {
        var list = For(kind);
        return Enumerable.Range(1, list.Count + 1).First(number => list.All(entry => entry.Instance != number));
    }

    public EntryDraft Add(EntryKind kind, string type)
    {
        var draft = EntrySchema.NewDraft(_strings, kind, type, NextInstance(kind), Pads);
        Insert(draft);
        return draft;
    }

    public void Insert(EntryDraft draft)
    {
        Attach(draft);
        switch (draft)
        {
            case EntryDraft<CameraConfiguration> camera:
                Cameras.Add(camera);
                break;
            case EntryDraft<VideoMixerConfiguration> mixer:
                Mixers.Add(mixer);
                break;
            case EntryDraft<InterfaceConfiguration> control:
                Interfaces.Add(control);
                break;
        }

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

        _ = entry switch
        {
            EntryDraft<CameraConfiguration> camera => Cameras.Remove(camera),
            EntryDraft<VideoMixerConfiguration> mixer => Mixers.Remove(mixer),
            EntryDraft<InterfaceConfiguration> control => Interfaces.Remove(control),
            _ => false,
        };

        IsDirty = true;
        Validate();
    }

    private IReadOnlyList<EntryDraft> For(EntryKind kind) => kind switch
    {
        EntryKind.Camera => Cameras,
        EntryKind.Mixer => Mixers,
        _ => Interfaces,
    };

    private static int? Mixer(EntryDraft entry) =>
        entry.Fields.OfType<NumberField>().FirstOrDefault(field => field.Key == "videoMixer")?.Number;

    private void Fill<TSection>(
        ObservableCollection<EntryDraft<TSection>> list,
        IEnumerable<TSection> entries,
        Func<TSection, EntryDraft<TSection>> draft)
        where TSection : EntryConfiguration
    {
        foreach (var entry in entries)
        {
            list.Add(draft(entry));
            Attach(list[^1]);
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
        // An entry the loader could not read is written back as it was, so it is shown with what is
        // wrong with it but does not stop the rest of the file being saved.
        foreach (var entry in list)
        {
            entry.Problem = entry.Unreadable;
        }

        foreach (var entry in list)
        {
            foreach (var field in entry.Fields.Where(field => field.Error is not null))
            {
                // A refusal is a sentence that names its property, a text error only a predicate.
                Note(entry, field.Error == field.Refusal
                    ? string.Format(CultureInfo.CurrentUICulture, _strings.Text("edit.issue.rule"), entry.Title, field.Error)
                    : string.Format(
                        CultureInfo.CurrentUICulture,
                        _strings.Text("edit.issue.field"),
                        entry.Title,
                        _strings.Text(field.Label),
                        field.Error));
            }

            foreach (var refusal in entry.Refusals)
            {
                Note(entry, string.Format(
                    CultureInfo.CurrentUICulture,
                    _strings.Text("edit.issue.rule"),
                    entry.Title,
                    $"{refusal.Path}: {refusal.Message}"));
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
                    _strings.Text("edit.issue.repeatedInstance"),
                    _strings.Text(section),
                    group.Key));
            }
        }
    }

    private void Flag(EntryDraft entry, string key, object argument) =>
        Note(entry, string.Format(
            CultureInfo.CurrentUICulture,
            _strings.Text(key),
            entry.Title,
            argument));

    private void Note(EntryDraft entry, string message)
    {
        entry.Problem ??= message;
        Issues.Add(message);
    }
}
