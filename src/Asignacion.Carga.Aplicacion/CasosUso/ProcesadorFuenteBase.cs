using Asignacion.Carga.Aplicacion.Interfaces;
using Asignacion.Carga.Aplicacion.Notificaciones;
using Asignacion.Carga.Dominio.Entidades;
using Asignacion.Carga.Dominio.Enums;
using Microsoft.Extensions.Logging;

namespace Asignacion.Carga.Aplicacion.CasosUso;

/// <summary>
/// Lo que comparten todos los procesadores: bitácora, carga por lotes, avisos y el manejo de errores.
/// </summary>
public abstract class ProcesadorFuenteBase : IProcesadorFuente
{
    protected IRepositorioBitacora Bitacora { get; }
    protected CargadorArchivo Cargador { get; }
    protected AvisadorCarga Avisador { get; }
    protected GeneradorNotificaciones Mensajes { get; }
    protected ILogger Log { get; }

    protected ProcesadorFuenteBase(
        IRepositorioBitacora bitacora, CargadorArchivo cargador, AvisadorCarga avisador, GeneradorNotificaciones mensajes, ILogger log)
    {
        Bitacora = bitacora;
        Cargador = cargador;
        Avisador = avisador;
        Mensajes = mensajes;
        Log = log;
    }

    public abstract TipoProceso Tipo { get; }

    public abstract Task<ResultadoProceso> ProcesarAsync(ConfiguracionFuente fuente, DateOnly fecha, bool forzar, CancellationToken ct);

    /// <summary>Sin archivo: deja constancia en la bitácora y avisa.</summary>
    protected async Task<ResultadoProceso> SinArchivoAsync(
        ConfiguracionFuente fuente, DateOnly fecha, string patron, IReadOnlyList<Destinatario> destinatarios, CancellationToken ct)
    {
        const string mensaje = "No se encontró archivo del día.";
        Log.LogWarning("Sin archivo: {Patron} en {Ruta}.", patron, fuente.RutaOrigen);
        await Bitacora.RegistrarEventoAsync(fuente.IdFuente, patron, fuente.RutaOrigen, fecha, EstatusCarga.SinArchivo, mensaje, ct);
        await Avisador.AvisarAsync(Mensajes.SinArchivo(fuente, fecha, patron), destinatarios, ct);
        return new ResultadoProceso(fuente.IdFuente, fuente.Nombre, EstatusCarga.SinArchivo, null, null, mensaje);
    }

    /// <summary>Ya estaba cargado: avisa y no hace nada más.</summary>
    protected async Task<ResultadoProceso> OmitidoAsync(
        ConfiguracionFuente fuente, ArchivoOrigen archivo, InicioCarga inicio, IReadOnlyList<Destinatario> destinatarios, CancellationToken ct)
    {
        Log.LogInformation("{Archivo} ya estaba cargado; no se carga nada.", archivo.Nombre);
        await Avisador.AvisarAsync(Mensajes.Omitido(fuente, archivo, inicio), destinatarios, ct);
        return new ResultadoProceso(fuente.IdFuente, fuente.Nombre, EstatusCarga.Omitido, archivo.Nombre, null, "El archivo ya estaba cargado.");
    }

    /// <summary>
    /// Deja el error en la bitácora (si la carga ya había iniciado la cierra como ERROR, lo que borra
    /// las filas parciales; si no, registra el evento), avisa y devuelve el resultado. Nunca lanza.
    /// </summary>
    protected async Task<ResultadoProceso> ErrorAsync(
        ConfiguracionFuente fuente, DateOnly fecha, string patron, ArchivoOrigen? archivo, InicioCarga? inicio,
        Exception error, IReadOnlyList<Destinatario> destinatarios, CancellationToken ct)
    {
        Log.LogError(error, "Falló la carga de la fuente {Fuente}, archivo {Archivo}.", fuente.Nombre, archivo?.Nombre ?? patron);

        ResultadoCarga? detalle = null;
        try
        {
            if (inicio is { IdLote: { } lote })
                detalle = await Bitacora.FinalizarCargaAsync(fuente.IdFuente, lote, EstatusCarga.Error, null, error.Message, ct);
            else
                await Bitacora.RegistrarEventoAsync(fuente.IdFuente, archivo?.Nombre ?? patron, archivo?.Carpeta ?? fuente.RutaOrigen,
                    fecha, EstatusCarga.Error, error.Message, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.LogError(ex, "Además, no se pudo registrar el error en la bitácora.");
        }

        await Avisador.AvisarAsync(Mensajes.Error(fuente, archivo?.Nombre, error.Message, inicio?.IdCarga), destinatarios, ct);
        return new ResultadoProceso(fuente.IdFuente, fuente.Nombre, EstatusCarga.Error, archivo?.Nombre, detalle, error.Message);
    }
}
