namespace Asignacion.Carga.Infraestructura.Notificaciones;

/// <summary>
/// Sección "Correo" del appsettings. La contraseña NUNCA va aquí:
/// se lee de la variable de entorno indicada en <see cref="VariableContrasena"/>.
/// </summary>
public sealed class OpcionesCorreo
{
    public const string Seccion = "Correo";

    /// <summary>false = no se envían correos (solo se escriben en el log).</summary>
    public bool Habilitado { get; set; } = true;

    public string Servidor { get; set; } = "smtp.gmail.com";
    public int Puerto { get; set; } = 587;

    /// <summary>StartTls (587), SslOnConnect (465), None o Auto.</summary>
    public string Seguridad { get; set; } = "StartTls";

    /// <summary>Cuenta con la que se inicia sesión en el servidor (vacío = sin autenticación, p. ej. relay interno).</summary>
    public string Usuario { get; set; } = string.Empty;

    /// <summary>Dirección que aparece como remitente; si está vacía se usa <see cref="Usuario"/>.</summary>
    public string Remitente { get; set; } = string.Empty;

    public string NombreRemitente { get; set; } = "Carga Asignacion";

    /// <summary>Variable de entorno que contiene la contraseña (de aplicación) de <see cref="Usuario"/>.</summary>
    public string VariableContrasena { get; set; } = "ASIGNACION_SMTP_PASSWORD";

    public int TiempoEsperaSegundos { get; set; } = 30;
}
