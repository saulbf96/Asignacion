using Asignacion.Carga.Aplicacion.CasosUso;
using Asignacion.Carga.Aplicacion.Interfaces;
using Asignacion.Carga.Aplicacion.Notificaciones;
using Asignacion.Carga.Dominio.Entidades;
using Asignacion.Carga.Dominio.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Asignacion.Carga.Consola;

/// <summary>
/// Ejecuta la carga, imprime el resumen y decide el código de salida.
/// Si el proceso no puede ni arrancar (p. ej. no hay conexión a la base), avisa por correo
/// a los destinatarios de respaldo del appsettings, porque los de la base no se pueden leer.
/// </summary>
public sealed class EjecutorConsola
{
    private readonly ProcesarCargaCasoUso _casoUso;
    private readonly INotificador _notificador;
    private readonly IConfiguration _configuracion;
    private readonly ILogger<EjecutorConsola> _log;

    public EjecutorConsola(ProcesarCargaCasoUso casoUso, INotificador notificador, IConfiguration configuracion, ILogger<EjecutorConsola> log)
    {
        _casoUso = casoUso;
        _notificador = notificador;
        _configuracion = configuracion;
        _log = log;
    }

    public async Task<int> EjecutarAsync(SolicitudCarga solicitud, CancellationToken ct)
    {
        _log.LogInformation("Inicio. Fuente: {Fuente}, fecha: {Fecha}, forzar: {Forzar}.",
            solicitud.IdFuente?.ToString() ?? "todas", solicitud.Fecha?.ToString("yyyy-MM-dd") ?? "hoy", solicitud.Forzar);
        try
        {
            var resultados = await _casoUso.EjecutarAsync(solicitud, ct);
            foreach (var r in resultados)
                _log.LogInformation("Resultado fuente {Fuente}: {Estatus} | archivo: {Archivo} | filas: {Filas} | {Mensaje}",
                    r.Fuente, r.Estatus, r.Archivo ?? "-", r.Detalle?.FilasInsertadas?.ToString("N0") ?? "-", r.Mensaje ?? "");

            var codigo = CodigoSalida.Desde(resultados);
            _log.LogInformation("Fin. Código de salida: {Codigo}.", codigo);
            return codigo;
        }
        catch (OperationCanceledException)
        {
            _log.LogWarning("Ejecución cancelada por el usuario.");
            return CodigoSalida.Error;
        }
        catch (Exception ex)
        {
            _log.LogCritical(ex, "La carga no pudo ejecutarse.");
            await AvisarFallaGeneralAsync(ex, ct);
            return CodigoSalida.Error;
        }
    }

    private async Task AvisarFallaGeneralAsync(Exception error, CancellationToken ct)
    {
        var correos = _configuracion.GetSection("Correo:DestinatariosRespaldo").Get<string[]>() ?? [];
        var destinatarios = new List<Destinatario>();
        foreach (var c in correos)
        {
            try { destinatarios.Add(new Destinatario(c)); }
            catch (Exception ex) { _log.LogWarning("Destinatario de respaldo inválido: {Mensaje}", ex.Message); }
        }
        if (destinatarios.Count == 0) return;

        var aviso = new Notificacion(TipoNotificacion.Error,
            "[Carga] ERROR general: el proceso no pudo ejecutarse",
            $"""
            La carga no pudo ejecutarse (no se procesó ninguna fuente).
            Causa probable: sin conexión a la base de datos o configuración inválida.
            Error: {error.Message}

            Equipo: {Environment.MachineName}
            """);
        try
        {
            await _notificador.EnviarAsync(aviso, destinatarios, ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Tampoco se pudo enviar el aviso de falla general.");
        }
    }
}
