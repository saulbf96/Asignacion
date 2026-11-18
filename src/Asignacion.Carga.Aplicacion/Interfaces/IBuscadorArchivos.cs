using Asignacion.Carga.Dominio.Entidades;

namespace Asignacion.Carga.Aplicacion.Interfaces;

/// <summary>
/// Busca en la ruta de origen los archivos de la fuente que corresponden a una fecha.
/// </summary>
public interface IBuscadorArchivos
{
    /// <summary>
    /// Archivos que coinciden con el patrón de la fuente para <paramref name="fecha"/>,
    /// sin incluir temporales de Office. Lista vacía si no hay ninguno.
    /// </summary>
    Task<IReadOnlyList<ArchivoOrigen>> BuscarAsync(ConfiguracionFuente fuente, DateOnly fecha, CancellationToken ct);
}
