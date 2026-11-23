using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.GenericFactory;

namespace Cgf.CameraControl.Core.Hmi;

public sealed class HmiFactory() : Factory<IHmi, InterfaceConfiguration>("interfaces");
