using Asignacion.Carga.Aplicacion.Interfaces;
using Asignacion.Carga.Dominio.Entidades;
using Microsoft.Extensions.Logging;

namespace Asignacion.Carga.Aplicacion.Notificaciones;

/// <summary>
/// Envía las notificaciones de una fuente. Un correo que no sale nunca detiene ni hace fallar la carga.
/// </summary>
public sealed class AvisadorCarga
{
    private readonly IRepositorioConfiguracion _configuracion;
    private readonly INotificador _notificador;
    private readonly ILogger<AvisadorCarga> _log;

    public AvisadorCarga(IRepositorioConfiguracion configuracion, INotificador notificador, ILogger<AvisadorCarga> log)
    {
        _configuracion = configuracion;
        _notificador = notificador;
        _log = log;
    }

    public async Task<IReadOnlyList<Destinatario>> ObtenerDestinatariosAsync(ConfiguracionFuente fuente, CancellationToken ct)
    {
        try
        {
            var destinatarios = await _configuracion.ObtenerDestinatariosAsync(fuente.IdFuente, ct);
            if (destinatarios.Count == 0)
                _log.LogWarning("La fuente {Fuente} no tiene correos activos; no se enviarán notificaciones.", fuente.Nombre);
            return destinatarios;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogError(ex, "No se pudieron leer los correos de notificación de la fuente {Fuente}.", fuente.Nombre);
            return [];
        }
    }

    public async Task AvisarAsync(Notificacion notificacion, IReadOnlyList<Destinatario> destinatarios, CancellationToken ct)
    {
        if (destinatarios.Count == 0) return;
        try
        {
            await _notificador.EnviarAsync(notificacion, destinatarios, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogError(ex, "No se pudo enviar la notificación '{Asunto}'.", notificacion.Asunto);
        }
    }
}
