using Asignacion.Carga.Dominio.Entidades;
using Asignacion.Carga.Dominio.Enums;

namespace Asignacion.Carga.Aplicacion.Interfaces;

/// <summary>
/// Registra el ciclo de vida de cada carga en ASIG.BITACORA_CARGA.
/// </summary>
public interface IRepositorioBitacora
{
    /// <summary>
    /// Registra el inicio. Si el archivo ya se cargó con éxito y <paramref name="forzar"/> es false,
    /// devuelve <see cref="EstatusCarga.Omitido"/> y no se debe cargar nada.
    /// </summary>
    Task<InicioCarga> IniciarCargaAsync(int idFuente, ArchivoOrigen archivo, bool forzar, CancellationToken ct);

    /// <summary>Cierra la carga como Exitoso o Error. Si es Error, se borran las filas parciales.</summary>
    Task<ResultadoCarga> FinalizarCargaAsync(
        int idFuente, Guid idLote, EstatusCarga estatus, int? filasLeidas, string? mensajeError, CancellationToken ct);

    /// <summary>
    /// Deja constancia de algo que pasó sin que la carga llegara a iniciar
    /// (<see cref="EstatusCarga.SinArchivo"/> o <see cref="EstatusCarga.Error"/>).
    /// </summary>
    Task RegistrarEventoAsync(
        int idFuente, string archivoOPatron, string? ruta, DateOnly fecha, EstatusCarga estatus, string? mensaje, CancellationToken ct);
}
