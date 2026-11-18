namespace Asignacion.Carga.Dominio.Enums;

/// <summary>
/// Momento del proceso que dispara un correo.
/// </summary>
public enum TipoNotificacion
{
    Inicio,
    Exitoso,
    Error,
    Omitido,
    SinArchivo
}
