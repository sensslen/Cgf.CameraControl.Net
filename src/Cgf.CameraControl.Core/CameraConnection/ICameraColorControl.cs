namespace Cgf.CameraControl.Core.CameraConnection;

public sealed record CameraColorState(bool IsManual, int RedGain, int BlueGain);

public interface ICameraColorControl
{
    IObservable<CameraColorState?> WhenColorChanged { get; }

    void RefreshColor();

    void OnePushWhiteBalance();

    void SetManualWhiteBalance(bool manual);

    void SetRedGain(int value);

    void SetBlueGain(int value);
}
