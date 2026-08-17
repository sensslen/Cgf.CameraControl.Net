using Cgf.CameraControl.Core.GenericFactory;

namespace Cgf.CameraControl.Core.VideoMixer;

public sealed class VideoMixerFactory() : Factory<IVideoMixer>("videoMixers");
