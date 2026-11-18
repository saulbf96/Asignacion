using Asignacion.Carga.Dominio.Entidades;

namespace Asignacion.Carga.Aplicacion.Interfaces;

/// <summary>
/// Lee la configuración guardada en la base (fuentes, correos y encabezados esperados).
/// </summary>
public interface IRepositorioConfiguracion
{
    /// <summary>Fuentes activas. Si <paramref name="idFuente"/> es null, todas.</summary>
    Task<IReadOnlyList<ConfiguracionFuente>> ObtenerFuentesAsync(int? idFuente, CancellationToken ct);

    /// <summary>Correos activos que reciben las notificaciones de la fuente.</summary>
    Task<IReadOnlyList<Destinatario>> ObtenerDestinatariosAsync(int idFuente, CancellationToken ct);

    /// <summary>Encabezados que debe traer el archivo de la fuente para poder cargarse.</summary>
    Task<IReadOnlyList<string>> ObtenerEncabezadosEsperadosAsync(int idFuente, CancellationToken ct);
}
