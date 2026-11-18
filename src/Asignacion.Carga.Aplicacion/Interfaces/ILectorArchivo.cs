using Asignacion.Carga.Dominio.Entidades;

namespace Asignacion.Carga.Aplicacion.Interfaces;

/// <summary>
/// Lee una hoja de un archivo (Excel u otro formato) sin cargarlo completo en memoria.
/// </summary>
public interface ILectorArchivo
{
    /// <summary>Encabezados de la fila <paramref name="filaEncabezado"/> (base 1) de la hoja.</summary>
    Task<IReadOnlyList<string?>> LeerEncabezadosAsync(ArchivoOrigen archivo, string hoja, int filaEncabezado, CancellationToken ct);

    /// <summary>Filas de datos que están debajo de los encabezados. Omite las filas vacías.</summary>
    IAsyncEnumerable<FilaArchivo> LeerFilasAsync(ArchivoOrigen archivo, string hoja, int filaEncabezado, CancellationToken ct);
}
