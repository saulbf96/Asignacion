using Asignacion.Carga.Dominio.Entidades;
using Asignacion.Carga.Dominio.Enums;

namespace Asignacion.Carga.Aplicacion.CasosUso;

/// <summary>
/// Qué pasó con una fuente en una ejecución.
/// </summary>
/// <param name="Archivo">Archivo procesado; null si no hubo.</param>
/// <param name="Detalle">Resultado de la bitácora cuando la carga llegó a finalizar.</param>
public sealed record ResultadoProceso(
    int IdFuente,
    string Fuente,
    EstatusCarga Estatus,
    string? Archivo,
    ResultadoCarga? Detalle,
    string? Mensaje)
{
    public bool FueError => Estatus == EstatusCarga.Error;
}
