using System.Data;
using Asignacion.Carga.Aplicacion.Interfaces;
using Asignacion.Carga.Dominio.Entidades;
using Asignacion.Carga.Dominio.Enums;
using Microsoft.Data.SqlClient;

namespace Asignacion.Carga.Infraestructura.Persistencia;

internal sealed class RepositorioBitacoraSql : IRepositorioBitacora
{
    private const int MaxMensaje = 4000;
    private readonly EjecutorSpCarga _sp;

    public RepositorioBitacoraSql(EjecutorSpCarga sp) => _sp = sp;

    public Task<InicioCarga> IniciarCargaAsync(int idFuente, ArchivoOrigen archivo, bool forzar, CancellationToken ct) =>
        _sp.ConsultarUnoAsync("INICIAR_CARGA",
            p =>
            {
                p.Agregar("@IdFuente", SqlDbType.Int, idFuente);
                p.Agregar("@ArchivoOrigen", SqlDbType.VarChar, archivo.Nombre, 260);
                p.Agregar("@RutaArchivo", SqlDbType.VarChar, archivo.Carpeta, 500);
                p.Agregar("@FechaArchivo", SqlDbType.Date, archivo.FechaArchivo.ToDateTime(TimeOnly.MinValue));
                p.Agregar("@Forzar", SqlDbType.Bit, forzar);
            },
            r => new InicioCarga(
                r.ValorNulable<Guid>("ID_LOTE"),
                r.GetInt64(r.GetOrdinal("ID_CARGA")),
                MapeoEstatus.Estatus(r.Valor<string>("ESTATUS")!),
                r.ValorNulable<int>("FILAS_ANTERIORES_ELIMINADAS") ?? 0,
                r.ValorNulable<DateTime>("FECHA_CARGA_PREVIA"),
                r.ValorNulable<int>("FILAS_CARGA_PREVIA")),
            ct);

    public Task<ResultadoCarga> FinalizarCargaAsync(
        int idFuente, Guid idLote, EstatusCarga estatus, int? filasLeidas, string? mensajeError, CancellationToken ct) =>
        _sp.ConsultarUnoAsync("FINALIZAR_CARGA",
            p =>
            {
                p.Agregar("@IdFuente", SqlDbType.Int, idFuente);
                p.Agregar("@IdLote", SqlDbType.UniqueIdentifier, idLote);
                p.Agregar("@Estatus", SqlDbType.VarChar, MapeoEstatus.Codigo(estatus), 15);
                p.Agregar("@FilasLeidas", SqlDbType.Int, filasLeidas);
                p.Agregar("@MensajeError", SqlDbType.NVarChar, mensajeError.Recortar(MaxMensaje), MaxMensaje);
            },
            MapearResultado,
            ct);

    public async Task RegistrarEventoAsync(
        int idFuente, string archivoOPatron, string? ruta, DateOnly fecha, EstatusCarga estatus, string? mensaje, CancellationToken ct) =>
        await _sp.ConsultarUnoAsync("REGISTRAR_EVENTO",
            p =>
            {
                p.Agregar("@IdFuente", SqlDbType.Int, idFuente);
                p.Agregar("@ArchivoOrigen", SqlDbType.VarChar, archivoOPatron.Recortar(260), 260);
                p.Agregar("@RutaArchivo", SqlDbType.VarChar, ruta.Recortar(500), 500);
                p.Agregar("@FechaArchivo", SqlDbType.Date, fecha.ToDateTime(TimeOnly.MinValue));
                p.Agregar("@Estatus", SqlDbType.VarChar, MapeoEstatus.Codigo(estatus), 15);
                p.Agregar("@MensajeError", SqlDbType.NVarChar, mensaje.Recortar(MaxMensaje), MaxMensaje);
            },
            r => r.GetInt64(r.GetOrdinal("ID_CARGA")),
            ct);

    private static ResultadoCarga MapearResultado(SqlDataReader r) => new(
        r.GetInt64(r.GetOrdinal("ID_CARGA")),
        r.Valor<string>("ARCHIVO_ORIGEN")!,
        MapeoEstatus.Estatus(r.Valor<string>("ESTATUS")!),
        r.ValorNulable<int>("FILAS_LEIDAS"),
        r.ValorNulable<int>("FILAS_INSERTADAS"),
        r.GetDateTime(r.GetOrdinal("FECHA_INICIO")),
        r.ValorNulable<DateTime>("FECHA_FIN"),
        r.Valor<string>("MENSAJE_ERROR"));
}
