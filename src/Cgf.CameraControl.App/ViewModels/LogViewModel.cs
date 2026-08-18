using System.Collections.ObjectModel;
using System.ComponentModel;
using Cgf.CameraControl.App.Hosting;
using Cgf.CameraControl.App.Localization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cgf.CameraControl.App.ViewModels;

/// A component to filter the log by, or every component when the name is absent. Source names are
/// the components' own, so the only translated entry is the one that means all of them.
public sealed class LogSource(string? name) : ViewModelBase
{
    public string? Name => name;

    public string Display => name ?? Localizer.Current["log.allSources"];

    public void Retranslate() => OnPropertyChanged(nameof(Display));
}

public sealed partial class LogViewModel : ViewModelBase, IDisposable
{
    // A desk left running for a service logs steadily. The window keeps the recent past, which is
    // what a fault is diagnosed from; anything older belongs in a file, not in memory.
    private const int Capacity = 1000;

    private readonly Queue<LogEntry> _history = new(Capacity);
    private readonly LogSource _everything = new(null);
    private readonly IDisposable _subscription;

    public LogViewModel(UiLogger logger)
    {
        Sources = [_everything];
        SelectedSource = _everything;
        _subscription = logger.WhenLogged.Bind(Append);
        Localizer.Current.PropertyChanged += OnLanguageChanged;
    }

    public ObservableCollection<LogEntry> Entries { get; } = [];

    public ObservableCollection<LogSource> Sources { get; }

    [ObservableProperty]
    public partial LogSource SelectedSource { get; set; }

    public void Dispose()
    {
        Localizer.Current.PropertyChanged -= OnLanguageChanged;
        _subscription.Dispose();
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e) => _everything.Retranslate();

    partial void OnSelectedSourceChanged(LogSource value)
    {
        Entries.Clear();
        foreach (var entry in _history.Where(Passes))
        {
            Entries.Insert(0, entry);
        }
    }

    private void Append(LogEntry entry)
    {
        if (_history.Count == Capacity)
        {
            var dropped = _history.Dequeue();
            if (Entries.Count > 0 && ReferenceEquals(Entries[^1], dropped))
            {
                Entries.RemoveAt(Entries.Count - 1);
            }
        }

        _history.Enqueue(entry);

        if (Sources.All(source => source.Name != entry.Source))
        {
            Sources.Add(new LogSource(entry.Source));
        }

        if (Passes(entry))
        {
            // Newest first. A live log that appends needs to scroll itself to stay useful, and an
            // auto-scroll fights whoever is reading the line above.
            Entries.Insert(0, entry);
        }
    }

    private bool Passes(LogEntry entry) => SelectedSource.Name is null || entry.Source == SelectedSource.Name;
}
