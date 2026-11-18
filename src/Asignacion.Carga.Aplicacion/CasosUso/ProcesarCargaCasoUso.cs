using Asignacion.Carga.Aplicacion.Interfaces;
using Asignacion.Carga.Aplicacion.Notificaciones;
using Asignacion.Carga.Dominio.Entidades;
using Asignacion.Carga.Dominio.Enums;
using Asignacion.Carga.Dominio.Servicios;
using Microsoft.Extensions.Logging;

namespace Asignacion.Carga.Aplicacion.CasosUso;

/// <summary>
/// Coordina la carga de cada fuente:
/// buscar archivo del día → iniciar en bitácora → validar encabezados →
/// insertar por lotes → finalizar → notificar en cada paso.
/// No conoce Excel, SQL ni SMTP: solo usa las interfaces.
/// </summary>
public sealed class ProcesarCargaCasoUso
{
    private readonly IRepositorioConfiguracion _configuracion;
    private readonly IRepositorioBitacora _bitacora;
    private readonly IRepositorioCarga _carga;
    private readonly IBuscadorArchivos _buscador;
    private readonly ILectorArchivo _lector;
    private readonly INotificador _notificador;
    private readonly GeneradorNotificaciones _mensajes;
    private readonly TimeProvider _reloj;
    private readonly ILogger<ProcesarCargaCasoUso> _log;

    public ProcesarCargaCasoUso(
        IRepositorioConfiguracion configuracion,
        IRepositorioBitacora bitacora,
        IRepositorioCarga carga,
        IBuscadorArchivos buscador,
        ILectorArchivo lector,
        INotificador notificador,
        GeneradorNotificaciones mensajes,
        TimeProvider reloj,
        ILogger<ProcesarCargaCasoUso> log)
    {
        _configuracion = configuracion;
        _bitacora = bitacora;
        _carga = carga;
        _buscador = buscador;
        _lector = lector;
        _notificador = notificador;
        _mensajes = mensajes;
        _reloj = reloj;
        _log = log;
    }

    public async Task<IReadOnlyList<ResultadoProceso>> EjecutarAsync(SolicitudCarga solicitud, CancellationToken ct = default)
    {
        var fecha = solicitud.Fecha ?? DateOnly.FromDateTime(_reloj.GetLocalNow().DateTime);
        var fuentes = await _configuracion.ObtenerFuentesAsync(solicitud.IdFuente, ct);

        if (fuentes.Count == 0)
            _log.LogWarning("No hay fuentes activas para procesar (IdFuente = {IdFuente}).", solicitud.IdFuente);

        var resultados = new List<ResultadoProceso>(fuentes.Count);
        foreach (var fuente in fuentes)
            resultados.Add(await ProcesarFuenteAsync(fuente, fecha, solicitud.Forzar, ct));

        return resultados;
    }

    private async Task<ResultadoProceso> ProcesarFuenteAsync(ConfiguracionFuente fuente, DateOnly fecha, bool forzar, CancellationToken ct)
    {
        _log.LogInformation("Procesando fuente {Fuente} (Id {IdFuente}) para el {Fecha:yyyy-MM-dd}.", fuente.Nombre, fuente.IdFuente, fecha);

        var destinatarios = await ObtenerDestinatariosAsync(fuente, ct);
        var patron = fuente.ResolverPatron(fecha);

        ArchivoOrigen? archivo = null;
        InicioCarga? inicio = null;
        try
        {
            // 1. Buscar el archivo del día
            var encontrados = await _buscador.BuscarAsync(fuente, fecha, ct);
            if (encontrados.Count == 0)
            {
                _log.LogWarning("Sin archivo del día: {Patron} en {Ruta}.", patron, fuente.RutaOrigen);
                await _bitacora.RegistrarEventoAsync(fuente.IdFuente, patron, fuente.RutaOrigen, fecha,
                    EstatusCarga.SinArchivo, "No se encontró archivo del día.", ct);
                await NotificarAsync(_mensajes.SinArchivo(fuente, fecha, patron), destinatarios, ct);
                return new ResultadoProceso(fuente.IdFuente, fuente.Nombre, EstatusCarga.SinArchivo, null, null, "No se encontró archivo del día.");
            }

            archivo = encontrados.MaxBy(a => a.FechaModificacion)!;
            if (encontrados.Count > 1)
                _log.LogWarning("Se encontraron {Total} archivos del día; se toma el más reciente: {Archivo}.", encontrados.Count, archivo.Nombre);

            // 2. Registrar inicio (o detectar que ya estaba cargado)
            inicio = await _bitacora.IniciarCargaAsync(fuente.IdFuente, archivo, forzar, ct);
            if (inicio.FueOmitida)
            {
                _log.LogInformation("{Archivo} ya estaba cargado; no se carga nada.", archivo.Nombre);
                await NotificarAsync(_mensajes.Omitido(fuente, archivo, inicio), destinatarios, ct);
                return new ResultadoProceso(fuente.IdFuente, fuente.Nombre, EstatusCarga.Omitido, archivo.Nombre, null, "El archivo ya estaba cargado.");
            }

            await NotificarAsync(_mensajes.Inicio(fuente, archivo, inicio, encontrados.Count), destinatarios, ct);

            // 3. Validar estructura y 4. insertar por lotes
            var filasLeidas = await CargarArchivoAsync(fuente, archivo, inicio.IdLote!.Value, ct);

            // 5. Finalizar
            var resultado = await _bitacora.FinalizarCargaAsync(fuente.IdFuente, inicio.IdLote.Value, EstatusCarga.Exitoso, filasLeidas, null, ct);
            _log.LogInformation("Carga terminada: {Archivo}, {Leidas} leídas, {Insertadas} insertadas.",
                archivo.Nombre, resultado.FilasLeidas, resultado.FilasInsertadas);
            await NotificarAsync(_mensajes.Exitoso(fuente, resultado), destinatarios, ct);

            return new ResultadoProceso(fuente.IdFuente, fuente.Nombre, EstatusCarga.Exitoso, archivo.Nombre, resultado, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogError(ex, "Falló la carga de la fuente {Fuente}, archivo {Archivo}.", fuente.Nombre, archivo?.Nombre ?? patron);
            var detalle = await RegistrarErrorAsync(fuente, fecha, patron, archivo, inicio, ex, ct);
            await NotificarAsync(_mensajes.Error(fuente, archivo?.Nombre, ex.Message, inicio?.IdCarga), destinatarios, ct);
            return new ResultadoProceso(fuente.IdFuente, fuente.Nombre, EstatusCarga.Error, archivo?.Nombre, detalle, ex.Message);
        }
    }

    private async Task<int> CargarArchivoAsync(ConfiguracionFuente fuente, ArchivoOrigen archivo, Guid idLote, CancellationToken ct)
    {
        var esperados = await _configuracion.ObtenerEncabezadosEsperadosAsync(fuente.IdFuente, ct);
        if (esperados.Count > 0)
        {
            var encontrados = await _lector.LeerEncabezadosAsync(archivo, fuente.Hoja, fuente.FilaEncabezado, ct);
            ValidadorEncabezados.Validar(archivo.Nombre, esperados, encontrados);
        }
        else
        {
            _log.LogWarning("La fuente {Fuente} no tiene encabezados esperados configurados; no se valida la estructura.", fuente.Nombre);
        }

        var leidas = 0;
        var lote = new List<FilaArchivo>(fuente.TamanoLote);
        await foreach (var fila in _lector.LeerFilasAsync(archivo, fuente.Hoja, fuente.FilaEncabezado, ct))
        {
            lote.Add(fila);
            leidas++;
            if (lote.Count == fuente.TamanoLote)
                await InsertarAsync(lote);
        }
        if (lote.Count > 0)
            await InsertarAsync(lote);

        return leidas;

        async Task InsertarAsync(List<FilaArchivo> filas)
        {
            await _carga.InsertarLoteAsync(fuente.IdFuente, idLote, filas.ToArray(), ct);
            _log.LogDebug("Lote insertado: {Filas} filas (acumulado {Leidas}).", filas.Count, leidas);
            filas.Clear();
        }
    }

    /// <summary>
    /// Deja el error en la bitácora: si la carga ya había iniciado, la cierra como ERROR
    /// (y así se borran las filas parciales); si no, registra el evento.
    /// Nunca lanza: un fallo aquí solo se escribe en el log.
    /// </summary>
    private async Task<ResultadoCarga?> RegistrarErrorAsync(
        ConfiguracionFuente fuente, DateOnly fecha, string patron, ArchivoOrigen? archivo, InicioCarga? inicio, Exception error, CancellationToken ct)
    {
        try
        {
            if (inicio is { IdLote: { } lote })
                return await _bitacora.FinalizarCargaAsync(fuente.IdFuente, lote, EstatusCarga.Error, null, error.Message, ct);

            await _bitacora.RegistrarEventoAsync(fuente.IdFuente, archivo?.Nombre ?? patron, archivo?.Carpeta ?? fuente.RutaOrigen,
                fecha, EstatusCarga.Error, error.Message, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogError(ex, "Además, no se pudo registrar el error en la bitácora.");
        }
        return null;
    }

    private async Task<IReadOnlyList<Destinatario>> ObtenerDestinatariosAsync(ConfiguracionFuente fuente, CancellationToken ct)
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

    /// <summary>Un correo que no sale nunca debe detener ni hacer fallar la carga.</summary>
    private async Task NotificarAsync(Notificacion notificacion, IReadOnlyList<Destinatario> destinatarios, CancellationToken ct)
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
