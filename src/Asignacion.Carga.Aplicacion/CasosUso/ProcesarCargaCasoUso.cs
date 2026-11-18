using Asignacion.Carga.Aplicacion.Interfaces;
using Asignacion.Carga.Dominio.Enums;
using Microsoft.Extensions.Logging;

namespace Asignacion.Carga.Aplicacion.CasosUso;

/// <summary>
/// Punto de entrada de la carga: lee las fuentes activas y entrega cada una al procesador de su tipo
/// (<see cref="TipoProceso.ArchivoDiario"/>, <see cref="TipoProceso.Unificar"/>, ...).
/// </summary>
public sealed class ProcesarCargaCasoUso
{
    private readonly IRepositorioConfiguracion _configuracion;
    private readonly IReadOnlyDictionary<TipoProceso, IProcesadorFuente> _procesadores;
    private readonly TimeProvider _reloj;
    private readonly ILogger<ProcesarCargaCasoUso> _log;

    public ProcesarCargaCasoUso(
        IRepositorioConfiguracion configuracion,
        IEnumerable<IProcesadorFuente> procesadores,
        TimeProvider reloj,
        ILogger<ProcesarCargaCasoUso> log)
    {
        _configuracion = configuracion;
        _procesadores = procesadores.ToDictionary(p => p.Tipo);
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
        {
            _log.LogInformation("Procesando fuente {Fuente} (Id {IdFuente}, {Tipo}) para el {Fecha:yyyy-MM-dd}.",
                fuente.Nombre, fuente.IdFuente, fuente.TipoProceso, fecha);

            if (!_procesadores.TryGetValue(fuente.TipoProceso, out var procesador))
            {
                var mensaje = $"No hay procesador registrado para el tipo {fuente.TipoProceso}.";
                _log.LogError(mensaje);
                resultados.Add(new ResultadoProceso(fuente.IdFuente, fuente.Nombre, EstatusCarga.Error, null, null, mensaje));
                continue;
            }

            resultados.Add(await procesador.ProcesarAsync(fuente, fecha, solicitud.Forzar, ct));
        }

        return resultados;
    }
}
