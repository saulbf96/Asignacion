namespace Asignacion.Carga.Infraestructura.Archivos;

/// <summary>
/// Sección "Unificacion" del appsettings. La contraseña de los zip NUNCA va aquí:
/// se lee de la variable de entorno indicada en <see cref="VariableContrasena"/>.
/// </summary>
public sealed class OpcionesUnificacion
{
    public const string Seccion = "Unificacion";
    public const string VariableContrasenaPorOmision = "ASIGNACION_ZIP_PASSWORD";

    /// <summary>Variable de entorno con la contraseña de los zip cifrados.</summary>
    public string VariableContrasena { get; set; } = VariableContrasenaPorOmision;

    /// <summary>
    /// Solo para ambientes de prueba: contraseña de los zip escrita en el appsettings (archivo fuera de git).
    /// Si la variable de entorno existe, tiene prioridad.
    /// </summary>
    public string Contrasena { get; set; } = string.Empty;

    /// <summary>Carpeta de trabajo; vacío = carpeta temporal de Windows.</summary>
    public string CarpetaTemporal { get; set; } = string.Empty;

    /// <summary>
    /// Columnas que se agregan al archivo de salida solo con encabezado y en blanco, por nombre de fuente
    /// (p. ej. "LEGAL"). No se insertan en la tabla: solo van en el Excel.
    /// </summary>
    public Dictionary<string, ColumnasEnBlanco> ColumnasEnBlanco { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Columnas en blanco de una fuente: se insertan, en este orden, después de <see cref="DespuesDe"/>.</summary>
public sealed class ColumnasEnBlanco
{
    public string DespuesDe { get; set; } = string.Empty;
    public List<string> Columnas { get; set; } = [];
}
