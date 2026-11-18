using Asignacion.Carga.Aplicacion.CasosUso;
using Asignacion.Carga.Aplicacion.Notificaciones;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Asignacion.Carga.Aplicacion;

public static class DependencyInjection
{
    /// <summary>Registra los casos de uso y servicios de la capa de aplicación.</summary>
    public static IServiceCollection AgregarAplicacion(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<GeneradorNotificaciones>();
        services.AddTransient<ProcesarCargaCasoUso>();
        return services;
    }
}
