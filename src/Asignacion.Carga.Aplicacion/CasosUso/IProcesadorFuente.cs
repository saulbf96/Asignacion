using Asignacion.Carga.Dominio.Entidades;
using Asignacion.Carga.Dominio.Enums;

namespace Asignacion.Carga.Aplicacion.CasosUso;

/// <summary>
/// Procesa una fuente según su <see cref="TipoProceso"/>.
/// Un tipo de proceso nuevo = una clase nueva que implementa esta interfaz (sin tocar las demás).
/// </summary>
public interface IProcesadorFuente
{
    TipoProceso Tipo { get; }

    /// <summary>Procesa la fuente completa. No lanza: los errores se devuelven en el resultado.</summary>
    Task<ResultadoProceso> ProcesarAsync(ConfiguracionFuente fuente, DateOnly fecha, bool forzar, CancellationToken ct);
}
