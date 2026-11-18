using Asignacion.Carga.Aplicacion.Interfaces;
using Asignacion.Carga.Aplicacion.Notificaciones;
using Asignacion.Carga.Dominio.Entidades;
using Asignacion.Carga.Dominio.Enums;
using Microsoft.Extensions.Logging;

namespace Asignacion.Carga.Aplicacion.CasosUso;

/// <summary>
/// UNIFICAR (p. ej. LEGAL):
/// unificar lo que dejó el usuario → insertar → guardar el unificado en la carpeta del mes →
/// cerrar la bitácora → vaciar la carpeta de entrada.
/// Si algo falla antes de cerrar la bitácora, no se borra nada de la entrada (se puede reintentar).
/// </summary>
public sealed class ProcesadorUnificado : ProcesadorFuenteBase
{
    private readonly IUnificadorArchivos _unificador;

    public ProcesadorUnificado(
        IUnificadorArchivos unificador, IRepositorioBitacora bitacora, CargadorArchivo cargador,
        AvisadorCarga avisador, GeneradorNotificaciones mensajes, ILogger<ProcesadorUnificado> log)
        : base(bitacora, cargador, avisador, mensajes, log)
    {
        _unificador = unificador;
    }

    public override TipoProceso Tipo => TipoProceso.Unificar;

    public override async Task<ResultadoProceso> ProcesarAsync(ConfiguracionFuente fuente, DateOnly fecha, bool forzar, CancellationToken ct)
    {
        var destinatarios = await Avisador.ObtenerDestinatariosAsync(fuente, ct);
        var nombreSalida = fuente.ResolverNombreSalida(fecha);
        ArchivoUnificado? unificado = null;
        InicioCarga? inicio = null;
        try
        {
            // 1. Unificar lo que dejó el usuario
            unificado = await _unificador.UnificarAsync(fuente, fecha, ct);
            if (unificado is null)
                return await SinArchivoAsync(fuente, fecha, fuente.RutaOrigen, destinatarios, ct);

            Log.LogInformation("Unificados {Archivos} archivos ({Repetidos} repetidos omitidos), {Filas} filas, desde {Entradas} elemento(s) de entrada.",
                unificado.ArchivosUnificados, unificado.ArchivosRepetidos, unificado.Filas, unificado.Entradas.Count);

            // 2. Registrar inicio. Si el usuario dejó archivos nuevos, se vuelve a cargar aunque ya exista el del día.
            inicio = await Bitacora.IniciarCargaAsync(fuente.IdFuente, unificado.Destino, forzar: true, ct);
            await Avisador.AvisarAsync(Mensajes.InicioUnificado(fuente, unificado, inicio), destinatarios, ct);

            // 3. Validar e insertar el unificado
            var leidas = await Cargador.CargarAsync(fuente, unificado.Lectura, unificado.Hoja, ArchivoUnificado.FilaEncabezado, inicio.IdLote!.Value, ct);

            // 4. Guardar el unificado en la carpeta del mes (si falla, la carga queda en ERROR)
            await _unificador.PublicarAsync(unificado, ct);

            // 5. Cerrar la bitácora
            var resultado = await Bitacora.FinalizarCargaAsync(fuente.IdFuente, inicio.IdLote.Value, EstatusCarga.Exitoso, leidas, null, ct);
            Log.LogInformation("Carga terminada: {Archivo}, {Leidas} leídas, {Insertadas} insertadas. Guardado en {Ruta}.",
                unificado.Destino.Nombre, resultado.FilasLeidas, resultado.FilasInsertadas, unificado.Destino.Carpeta);

            // 6. Vaciar la carpeta de entrada (si algo no se puede borrar, solo se avisa)
            var noBorrados = await VaciarEntradaAsync(fuente, unificado, ct);

            await Avisador.AvisarAsync(Mensajes.ExitosoUnificado(fuente, resultado, unificado, fuente.BorrarEntrada, noBorrados), destinatarios, ct);
            return new ResultadoProceso(fuente.IdFuente, fuente.Nombre, EstatusCarga.Exitoso, unificado.Destino.Nombre, resultado,
                noBorrados.Count > 0 ? $"No se pudo borrar de la entrada: {string.Join(", ", noBorrados)}" : null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return await ErrorAsync(fuente, fecha, fuente.RutaOrigen, unificado?.Destino ?? new ArchivoOrigen(nombreSalida, fuente.RutaOrigen, fecha, DateTime.Now),
                inicio, ex, destinatarios, ct);
        }
        finally
        {
            if (unificado is not null)
                await _unificador.DescartarTemporalesAsync(unificado);
        }
    }

    private async Task<IReadOnlyList<string>> VaciarEntradaAsync(ConfiguracionFuente fuente, ArchivoUnificado unificado, CancellationToken ct)
    {
        if (!fuente.BorrarEntrada) return [];
        try
        {
            var noBorrados = await _unificador.VaciarEntradaAsync(unificado, ct);
            foreach (var n in noBorrados)
                Log.LogWarning("No se pudo borrar de la entrada: {Elemento}", n);
            return noBorrados;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.LogError(ex, "No se pudo vaciar la carpeta de entrada {Ruta}.", fuente.RutaOrigen);
            return [.. unificado.Entradas];
        }
    }
}
