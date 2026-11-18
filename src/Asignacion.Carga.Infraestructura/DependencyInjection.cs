using Asignacion.Carga.Aplicacion.Interfaces;
using Asignacion.Carga.Infraestructura.Archivos;
using Asignacion.Carga.Infraestructura.Excel;
using Asignacion.Carga.Infraestructura.Notificaciones;
using Asignacion.Carga.Infraestructura.Persistencia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Asignacion.Carga.Infraestructura;

public static class DependencyInjection
{
    /// <summary>
    /// Registra las implementaciones reales: SQL Server (ASIG.SP_CARGA), disco/red, Excel, zip y correo.
    /// Lee las secciones "BaseDatos", "Correo" y "Unificacion" de la configuración.
    /// </summary>
    public static IServiceCollection AgregarInfraestructura(this IServiceCollection services, IConfiguration configuracion)
    {
        services.Configure<OpcionesBaseDatos>(configuracion.GetSection(OpcionesBaseDatos.Seccion));
        services.Configure<OpcionesCorreo>(configuracion.GetSection(OpcionesCorreo.Seccion));
        services.Configure<OpcionesUnificacion>(configuracion.GetSection(OpcionesUnificacion.Seccion));
        services.AddSingleton<IUnificadorArchivos, UnificadorArchivos>();

        services.AddSingleton<EjecutorSpCarga>();
        services.AddSingleton<IRepositorioConfiguracion, RepositorioConfiguracionSql>();
        services.AddSingleton<IRepositorioBitacora, RepositorioBitacoraSql>();
        services.AddSingleton<IRepositorioCarga, RepositorioCargaSql>();
        services.AddSingleton<IBuscadorArchivos, BuscadorArchivosDisco>();
        services.AddSingleton<ILectorArchivo, LectorExcelOpenXml>();
        services.AddSingleton<INotificador, NotificadorCorreoMailKit>();

        return services;
    }
}
