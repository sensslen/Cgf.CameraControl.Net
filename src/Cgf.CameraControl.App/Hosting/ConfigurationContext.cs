using System.Text.Json.Serialization;
using Cgf.CameraControl.Atem.VideoMixer.Blackmagicdesign;
using Cgf.CameraControl.Cameras.SignalrPtzLanc.Camera;
using Cgf.CameraControl.Cameras.ViscaOverIp.Camera;
using Cgf.CameraControl.Cameras.WebsocketPtzLanc.Camera;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.VideoMixer.Passthrough;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

namespace Cgf.CameraControl.App.Hosting;

/// The metadata of every configuration record, generated here because this is the one project that
/// references every plugin. Which type string reads as which record is registered in
/// <see cref="AppHost.Schema"/>, next to the builders that claim them.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(CameraConfiguration))]
[JsonSerializable(typeof(VideoMixerConfiguration))]
[JsonSerializable(typeof(InterfaceConfiguration))]
[JsonSerializable(typeof(WebsocketPtzLancCameraConfiguration))]
[JsonSerializable(typeof(SignalrPtzLancCameraConfiguration))]
[JsonSerializable(typeof(ViscaOverIpCameraConfiguration))]
[JsonSerializable(typeof(AtemConfiguration))]
[JsonSerializable(typeof(PassthroughConfiguration))]
[JsonSerializable(typeof(GamepadConfiguration))]
[JsonSerializable(typeof(LogitechF310Configuration))]
[JsonSerializable(typeof(LogitechF710Configuration))]
[JsonSerializable(typeof(LogitechRumblepad2Configuration))]
[JsonSerializable(typeof(KeyboardConfiguration))]
public sealed partial class ConfigurationContext : JsonSerializerContext;
