using Asignacion.Carga.Dominio.Enums;

namespace Asignacion.Carga.Aplicacion.Notificaciones;

/// <summary>
/// Mensaje listo para enviar. El cuerpo es texto plano (una línea por dato).
/// </summary>
public sealed record Notificacion(TipoNotificacion Tipo, string Asunto, string Cuerpo);
