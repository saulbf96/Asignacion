using Asignacion.Carga.Dominio.Entidades;

namespace Asignacion.Carga.Aplicacion.Interfaces;

/// <summary>
/// Inserta los datos del archivo en la tabla destino de la fuente.
/// </summary>
public interface IRepositorioCarga
{
    /// <summary>Inserta un lote de filas dentro de la carga <paramref name="idLote"/>. Devuelve las filas insertadas.</summary>
    Task<int> InsertarLoteAsync(int idFuente, Guid idLote, IReadOnlyList<FilaArchivo> filas, CancellationToken ct);
}
