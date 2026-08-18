using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Cgf.CameraControl.App.Hosting;
using Cgf.CameraControl.App.Localization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cgf.CameraControl.App.ViewModels;

/// A component to filter the log by, or every component when the name is absent. Source names are
/// the components' own, so the only translated entry is the one that means all of them.
public sealed class LogSource(string? name)
{
    public string? Name => name;

    public IObservable<string> Display =>
        name is null ? Localizer.Current["log.allSources"] : Observable.Return(name);
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
    }

    public ObservableCollection<LogEntry> Entries { get; } = [];

    public ObservableCollection<LogSource> Sources { get; }

    [ObservableProperty]
    public partial LogSource SelectedSource { get; set; }

    public void Dispose() => _subscription.Dispose();

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
