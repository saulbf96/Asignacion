using Asignacion.Carga.Dominio.Enums;

namespace Asignacion.Carga.Dominio.Entidades;

/// <summary>
/// Respuesta al registrar el inicio de una carga en la bitácora.
/// Si el archivo ya estaba cargado, <see cref="Estatus"/> es <see cref="EstatusCarga.Omitido"/>
/// y no hay lote para insertar.
/// </summary>
public sealed record InicioCarga(
    Guid? IdLote,
    long IdCarga,
    EstatusCarga Estatus,
    int FilasAnterioresEliminadas,
    DateTime? FechaCargaPrevia,
    int? FilasCargaPrevia)
{
    public bool FueOmitida => Estatus == EstatusCarga.Omitido;
}
