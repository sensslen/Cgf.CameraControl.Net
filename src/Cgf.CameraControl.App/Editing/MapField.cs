using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json.Nodes;
using Cgf.CameraControl.App.Localization;
using Cgf.CameraControl.App.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cgf.CameraControl.App.Editing;

/// A number to a number, which is what `cameraMap` is: a mixer input on the left, the camera behind
/// it on the right. Rows rather than a text box, because the thing being edited is a list of pairs and
/// a JSON object typed by hand is a syntax error waiting to be reported against the whole entry.
public sealed partial class MapField : Field
{
    public MapField(JsonObject entry, string key, string label, string leftLabel, string rightLabel)
        : base(entry, key, label)
    {
        LeftLabel = leftLabel;
        RightLabel = rightLabel;

        foreach (var pair in entry[key] as JsonObject ?? [])
        {
            Add(pair.Key, pair.Value?.ToString() ?? string.Empty);
        }

        Ready();
    }

    public string LeftLabel { get; }

    public string RightLabel { get; }

    public IObservable<string> LeftCaption => Localizer.Current[LeftLabel];

    public IObservable<string> RightCaption => Localizer.Current[RightLabel];

    public ObservableCollection<MapRow> Rows { get; } = [];

    /// The camera instances this map names, for the document to check against the cameras that exist.
    public IEnumerable<int> Targets => Rows.Select(row => Number(row.Value)).OfType<int>();

    protected internal override void Validate()
    {
        var keys = Rows.Select(row => Number(row.Key)).OfType<int>().ToList();
        Error = Rows.Any(row => Number(row.Key) is null or < 1 || Number(row.Value) is null or < 1)
            ? Localizer.Current.Text("edit.mapNeedsNumbers")
            : keys.Count != keys.Distinct().Count()
                ? Localizer.Current.Text("edit.mapRepeatsAnInput")
                : null;
    }

    private static int? Number(string text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentUICulture, out var parsed) ? parsed : null;

    /// A new row is proposed rather than left blank: the lowest input nothing has taken, and the
    /// camera of the same number, which is how every desk that has not been rearranged is wired.
    [RelayCommand]
    private void AddRow()
    {
        var taken = Rows.Select(row => Number(row.Key)).OfType<int>().ToHashSet();
        var input = Enumerable.Range(1, taken.Count + 1).First(number => !taken.Contains(number));
        Add(
            input.ToString(CultureInfo.CurrentUICulture),
            input.ToString(CultureInfo.CurrentUICulture));
        Save();
    }

    [RelayCommand]
    private void RemoveRow(MapRow row)
    {
        row.PropertyChanged -= OnRowChanged;
        Rows.Remove(row);
        Save();
    }

    private void Add(string key, string value)
    {
        var row = new MapRow { Key = key, Value = value };
        row.PropertyChanged += OnRowChanged;
        Rows.Add(row);
    }

    private void OnRowChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => Save();

    /// Rebuilt whole rather than patched, because a row whose left side changed has moved to a
    /// different key and there is nothing to patch.
    private void Save()
    {
        var map = new JsonObject();
        foreach (var row in Rows)
        {
            if (Number(row.Key) is { } input && Number(row.Value) is { } camera && !map.ContainsKey(row.Key))
            {
                map[input.ToString(CultureInfo.InvariantCulture)] = JsonValue.Create(camera);
            }
        }

        Write(map);
    }
}

public sealed partial class MapRow : ViewModelBase
{
    [ObservableProperty]
    public partial string Key { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Value { get; set; } = string.Empty;
}
