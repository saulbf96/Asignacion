namespace Asignacion.Carga.Infraestructura.Persistencia;

/// <summary>
/// Sección "BaseDatos" del appsettings.
/// </summary>
public sealed class OpcionesBaseDatos
{
    public const string Seccion = "BaseDatos";

    /// <summary>Cadena de conexión (Windows Authentication, sin contraseñas).</summary>
    public string CadenaConexion { get; set; } = string.Empty;

    /// <summary>Procedimiento que concentra toda la carga.</summary>
    public string Procedimiento { get; set; } = "ASIG.SP_CARGA";

    /// <summary>Tiempo máximo por llamada al SP.</summary>
    public int TiempoEsperaSegundos { get; set; } = 300;
}
