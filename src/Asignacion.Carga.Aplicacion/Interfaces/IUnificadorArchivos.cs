using Asignacion.Carga.Dominio.Entidades;

namespace Asignacion.Carga.Aplicacion.Interfaces;

/// <summary>
/// Toma lo que el usuario dejó en la carpeta de entrada (zip con o sin contraseña, zip dentro de zip,
/// carpetas, Excel sueltos) y lo une en un solo archivo.
/// </summary>
public interface IUnificadorArchivos
{
    /// <summary>Unifica en una carpeta temporal. Null si la carpeta de entrada está vacía.</summary>
    Task<ArchivoUnificado?> UnificarAsync(ConfiguracionFuente fuente, DateOnly fecha, CancellationToken ct);

    /// <summary>Copia el unificado a su carpeta final (crea la carpeta del mes si no existe; reemplaza si ya existe).</summary>
    Task PublicarAsync(ArchivoUnificado unificado, CancellationToken ct);

    /// <summary>Borra de la carpeta de entrada lo que se unificó. Devuelve lo que no se pudo borrar.</summary>
    Task<IReadOnlyList<string>> VaciarEntradaAsync(ArchivoUnificado unificado, CancellationToken ct);

    /// <summary>Borra la carpeta temporal. Nunca lanza.</summary>
    Task DescartarTemporalesAsync(ArchivoUnificado unificado);
}
