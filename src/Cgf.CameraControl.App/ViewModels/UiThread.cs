using Avalonia.Threading;

namespace Cgf.CameraControl.App.ViewModels;

internal static class UiThread
{
    /// Domain state arrives on whichever thread produced it: the SDL pump, a websocket reader, an
    /// ATEM dataflow block. Posting rather than checking first also keeps a subscription from
    /// re-entering the view model while it is still handling the previous value.
    public static IDisposable Bind<T>(this IObservable<T> source, Action<T> onNext) =>
        source.Subscribe(value => Dispatcher.UIThread.Post(() => onNext(value)));
}
