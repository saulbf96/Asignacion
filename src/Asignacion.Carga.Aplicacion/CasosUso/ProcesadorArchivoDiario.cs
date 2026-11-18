using Asignacion.Carga.Aplicacion.Interfaces;
using Asignacion.Carga.Aplicacion.Notificaciones;
using Asignacion.Carga.Dominio.Entidades;
using Asignacion.Carga.Dominio.Enums;
using Microsoft.Extensions.Logging;

namespace Asignacion.Carga.Aplicacion.CasosUso;

/// <summary>
/// ARCHIVO_DIARIO (p. ej. MOVILIDAD): busca el archivo del día por patrón y lo carga.
/// No mueve ni borra nada de la carpeta de origen.
/// </summary>
public sealed class ProcesadorArchivoDiario : ProcesadorFuenteBase
{
    private readonly IBuscadorArchivos _buscador;

    public ProcesadorArchivoDiario(
        IBuscadorArchivos buscador, IRepositorioBitacora bitacora, CargadorArchivo cargador,
        AvisadorCarga avisador, GeneradorNotificaciones mensajes, ILogger<ProcesadorArchivoDiario> log)
        : base(bitacora, cargador, avisador, mensajes, log)
    {
        _buscador = buscador;
    }

    public override TipoProceso Tipo => TipoProceso.ArchivoDiario;

    public override async Task<ResultadoProceso> ProcesarAsync(ConfiguracionFuente fuente, DateOnly fecha, bool forzar, CancellationToken ct)
    {
        var destinatarios = await Avisador.ObtenerDestinatariosAsync(fuente, ct);
        var patron = fuente.ResolverPatron(fecha);
        ArchivoOrigen? archivo = null;
        InicioCarga? inicio = null;
        try
        {
            // 1. Buscar el archivo del día (si hay varios, el más reciente)
            var encontrados = await _buscador.BuscarAsync(fuente, fecha, ct);
            if (encontrados.Count == 0)
                return await SinArchivoAsync(fuente, fecha, patron, destinatarios, ct);

            archivo = encontrados.MaxBy(a => a.FechaModificacion)!;
            if (encontrados.Count > 1)
                Log.LogWarning("Se encontraron {Total} archivos del día; se toma el más reciente: {Archivo}.", encontrados.Count, archivo.Nombre);

            // 2. Registrar inicio (o detectar que ya estaba cargado)
            inicio = await Bitacora.IniciarCargaAsync(fuente.IdFuente, archivo, forzar, ct);
            if (inicio.FueOmitida)
                return await OmitidoAsync(fuente, archivo, inicio, destinatarios, ct);

            await Avisador.AvisarAsync(Mensajes.Inicio(fuente, archivo, inicio, encontrados.Count), destinatarios, ct);

            // 3. Validar e insertar por lotes
            var leidas = await Cargador.CargarAsync(fuente, archivo, fuente.Hoja, fuente.FilaEncabezado, inicio.IdLote!.Value, ct);

            // 4. Finalizar
            var resultado = await Bitacora.FinalizarCargaAsync(fuente.IdFuente, inicio.IdLote.Value, EstatusCarga.Exitoso, leidas, null, ct);
            Log.LogInformation("Carga terminada: {Archivo}, {Leidas} leídas, {Insertadas} insertadas.",
                archivo.Nombre, resultado.FilasLeidas, resultado.FilasInsertadas);
            await Avisador.AvisarAsync(Mensajes.Exitoso(fuente, resultado), destinatarios, ct);

            return new ResultadoProceso(fuente.IdFuente, fuente.Nombre, EstatusCarga.Exitoso, archivo.Nombre, resultado, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return await ErrorAsync(fuente, fecha, patron, archivo, inicio, ex, destinatarios, ct);
        }
    }
}
