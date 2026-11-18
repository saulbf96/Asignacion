namespace Asignacion.Carga.Dominio.Entidades;

/// <summary>
/// Archivo encontrado en la ruta de origen que se va a cargar.
/// </summary>
/// <param name="Nombre">Nombre del archivo con extensión (lo que se guarda en ARCHIVO_ORIGEN).</param>
/// <param name="Carpeta">Carpeta donde se encontró (lo que se guarda en RUTA_ARCHIVO).</param>
/// <param name="FechaArchivo">Día al que corresponde el archivo.</param>
/// <param name="FechaModificacion">Última modificación en disco; sirve para elegir el más reciente.</param>
public sealed record ArchivoOrigen(string Nombre, string Carpeta, DateOnly FechaArchivo, DateTime FechaModificacion)
{
    public string RutaCompleta => Path.Combine(Carpeta, Nombre);

    /// <summary>
    /// Archivos de bloqueo que crea Office mientras alguien tiene el Excel abierto (<c>~$...</c>).
    /// Nunca se deben cargar.
    /// </summary>
    public static bool EsTemporalDeOffice(string nombreArchivo) =>
        Path.GetFileName(nombreArchivo).StartsWith("~$", StringComparison.Ordinal);
}
