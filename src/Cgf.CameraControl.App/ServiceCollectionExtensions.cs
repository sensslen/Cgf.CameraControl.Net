using Cgf.CameraControl.App.Hosting;
using Cgf.CameraControl.App.Localization;
using Cgf.CameraControl.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Cgf.CameraControl.App;

/// The application's services, each registered by its own type. The published binary is trimmed, so
/// nothing here is found by scanning an assembly.
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCommonServices(this IServiceCollection services)
    {
        services.AddSingleton<ILocalizer, Localizer>();
        services.AddSingleton<AppHost>();
        services.AddSingleton<MainViewModel>();
        return services;
    }
}
