using Asignacion.Carga.Aplicacion.Interfaces;
using Asignacion.Carga.Dominio.Entidades;
using Asignacion.Carga.Dominio.Servicios;
using Microsoft.Extensions.Logging;

namespace Asignacion.Carga.Aplicacion.CasosUso;

/// <summary>
/// Valida que el archivo traiga los encabezados esperados y lo inserta por lotes.
/// Lo comparten todos los tipos de proceso.
/// </summary>
public sealed class CargadorArchivo
{
    private readonly IRepositorioConfiguracion _configuracion;
    private readonly ILectorArchivo _lector;
    private readonly IRepositorioCarga _carga;
    private readonly ILogger<CargadorArchivo> _log;

    public CargadorArchivo(IRepositorioConfiguracion configuracion, ILectorArchivo lector, IRepositorioCarga carga, ILogger<CargadorArchivo> log)
    {
        _configuracion = configuracion;
        _lector = lector;
        _carga = carga;
        _log = log;
    }

    /// <summary>Devuelve las filas leídas.</summary>
    public async Task<int> CargarAsync(
        ConfiguracionFuente fuente, ArchivoOrigen archivo, string hoja, int filaEncabezado, Guid idLote, CancellationToken ct)
    {
        var esperados = await _configuracion.ObtenerEncabezadosEsperadosAsync(fuente.IdFuente, ct);
        if (esperados.Count > 0)
        {
            var encontrados = await _lector.LeerEncabezadosAsync(archivo, hoja, filaEncabezado, ct);
            ValidadorEncabezados.Validar(archivo.Nombre, esperados, encontrados);
        }
        else
        {
            _log.LogWarning("La fuente {Fuente} no tiene encabezados esperados configurados; no se valida la estructura.", fuente.Nombre);
        }

        var leidas = 0;
        var lote = new List<FilaArchivo>(fuente.TamanoLote);
        await foreach (var fila in _lector.LeerFilasAsync(archivo, hoja, filaEncabezado, ct))
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
}
