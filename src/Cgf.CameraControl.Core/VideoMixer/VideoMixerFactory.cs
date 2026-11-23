using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.GenericFactory;

namespace Cgf.CameraControl.Core.VideoMixer;

public sealed class VideoMixerFactory() : Factory<IVideoMixer, VideoMixerConfiguration>("videoMixers");
