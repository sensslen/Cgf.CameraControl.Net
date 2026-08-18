using System.Collections.ObjectModel;
using Cgf.CameraControl.App.Hosting;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cgf.CameraControl.App.ViewModels;

public sealed partial class LogViewModel : ViewModelBase, IDisposable
{
    public const string AllSources = "All";

    // A desk left running for a service logs steadily. The window keeps the recent past, which is
    // what a fault is diagnosed from; anything older belongs in a file, not in memory.
    private const int Capacity = 1000;

    private readonly Queue<LogEntry> _history = new(Capacity);
    private readonly IDisposable _subscription;

    public LogViewModel(UiLogger logger) => _subscription = logger.WhenLogged.Bind(Append);

    public ObservableCollection<LogEntry> Entries { get; } = [];

    public ObservableCollection<string> Sources { get; } = [AllSources];

    [ObservableProperty]
    public partial string SelectedSource { get; set; } = AllSources;

    public void Dispose() => _subscription.Dispose();

    partial void OnSelectedSourceChanged(string value)
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

        if (!Sources.Contains(entry.Source))
        {
            Sources.Add(entry.Source);
        }

        if (Passes(entry))
        {
            // Newest first. A live log that appends needs to scroll itself to stay useful, and an
            // auto-scroll fights whoever is reading the line above.
            Entries.Insert(0, entry);
        }
    }

    private bool Passes(LogEntry entry) => SelectedSource == AllSources || entry.Source == SelectedSource;
}
