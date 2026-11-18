using Asignacion.Carga.Aplicacion.Notificaciones;
using Asignacion.Carga.Dominio.Entidades;

namespace Asignacion.Carga.Aplicacion.Interfaces;

/// <summary>
/// Envía una notificación (por correo u otro medio) a los destinatarios.
/// </summary>
public interface INotificador
{
    Task EnviarAsync(Notificacion notificacion, IReadOnlyList<Destinatario> destinatarios, CancellationToken ct);
}
