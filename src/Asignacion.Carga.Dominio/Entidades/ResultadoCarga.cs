using Asignacion.Carga.Dominio.Enums;

namespace Asignacion.Carga.Dominio.Entidades;

/// <summary>
/// Resultado final de una carga, tal como queda en la bitácora.
/// </summary>
public sealed record ResultadoCarga(
    long IdCarga,
    string ArchivoOrigen,
    EstatusCarga Estatus,
    int? FilasLeidas,
    int? FilasInsertadas,
    DateTime FechaInicio,
    DateTime? FechaFin,
    string? MensajeError)
{
    public TimeSpan? Duracion => FechaFin - FechaInicio;

    /// <summary>Se insertaron todas las filas que se leyeron del archivo.</summary>
    public bool EstaCompleta => FilasLeidas.HasValue && FilasLeidas == FilasInsertadas;
}
