using System.Data;
using Asignacion.Carga.Aplicacion.Interfaces;
using Asignacion.Carga.Dominio.Entidades;
using Asignacion.Carga.Dominio.Excepciones;
using Microsoft.Extensions.Logging;

namespace Asignacion.Carga.Infraestructura.Persistencia;

internal sealed class RepositorioConfiguracionSql : IRepositorioConfiguracion
{
    private readonly EjecutorSpCarga _sp;
    private readonly ILogger<RepositorioConfiguracionSql> _log;

    public RepositorioConfiguracionSql(EjecutorSpCarga sp, ILogger<RepositorioConfiguracionSql> log)
    {
        _sp = sp;
        _log = log;
    }

    public Task<IReadOnlyList<ConfiguracionFuente>> ObtenerFuentesAsync(int? idFuente, CancellationToken ct) =>
        _sp.ConsultarAsync("OBTENER_CONFIG",
            p => p.Agregar("@IdFuente", SqlDbType.Int, idFuente),
            r => new ConfiguracionFuente(
                r.GetInt32(r.GetOrdinal("ID_FUENTE")),
                r.Valor<string>("NOMBRE")!,
                r.Valor<string>("RUTA_ORIGEN")!,
                r.Valor<string>("PATRON_ARCHIVO")!,
                r.Valor<string>("HOJA")!,
                r.GetInt32(r.GetOrdinal("FILA_ENCABEZADO")),
                r.Valor<string>("TABLA_DESTINO")!,
                r.GetInt32(r.GetOrdinal("TAMANO_LOTE"))),
            ct);

    public async Task<IReadOnlyList<Destinatario>> ObtenerDestinatariosAsync(int idFuente, CancellationToken ct)
    {
        var filas = await _sp.ConsultarAsync("OBTENER_NOTIFICACION",
            p => p.Agregar("@IdFuente", SqlDbType.Int, idFuente),
            r => (Correo: r.Valor<string>("CORREO") ?? "", Nombre: r.Valor<string>("NOMBRE")),
            ct);

        // Un correo mal escrito en la tabla no debe impedir avisar a los demás
        var destinatarios = new List<Destinatario>(filas.Count);
        foreach (var (correo, nombre) in filas)
        {
            try { destinatarios.Add(new Destinatario(correo, nombre)); }
            catch (ConfiguracionInvalidaException ex) { _log.LogWarning("Se ignora un destinatario: {Mensaje}", ex.Message); }
        }
        return destinatarios;
    }

    public Task<IReadOnlyList<string>> ObtenerEncabezadosEsperadosAsync(int idFuente, CancellationToken ct) =>
        _sp.ConsultarAsync("OBTENER_ENCABEZADOS",
            p => p.Agregar("@IdFuente", SqlDbType.Int, idFuente),
            r => r.Valor<string>("ENCABEZADO")!,
            ct);
}
