using Cgf.CameraControl.Core.GenericFactory;

namespace Cgf.CameraControl.Core.CameraConnection;

public sealed class CameraConnectionFactory() : Factory<ICameraConnection>("cams");
