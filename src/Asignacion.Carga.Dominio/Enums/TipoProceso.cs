namespace Asignacion.Carga.Dominio.Enums;

/// <summary>
/// Cómo se obtiene el archivo de una fuente (CONFIG_FUENTE.TIPO_PROCESO).
/// </summary>
public enum TipoProceso
{
    /// <summary>Se carga directo el archivo del día que coincide con el patrón (p. ej. MOVILIDAD).</summary>
    ArchivoDiario,

    /// <summary>
    /// Se toma todo lo que dejó el usuario (zip, carpetas, Excel sueltos), se unifica en un solo
    /// archivo, se carga, se guarda el unificado y se vacía la carpeta de entrada (p. ej. LEGAL).
    /// </summary>
    Unificar
}
