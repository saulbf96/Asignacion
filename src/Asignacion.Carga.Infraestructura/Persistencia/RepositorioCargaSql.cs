using System.Data;
using Asignacion.Carga.Aplicacion.Interfaces;
using Asignacion.Carga.Dominio.Entidades;

namespace Asignacion.Carga.Infraestructura.Persistencia;

internal sealed class RepositorioCargaSql : IRepositorioCarga
{
    private readonly EjecutorSpCarga _sp;

    public RepositorioCargaSql(EjecutorSpCarga sp) => _sp = sp;

    public Task<int> InsertarLoteAsync(int idFuente, Guid idLote, IReadOnlyList<FilaArchivo> filas, CancellationToken ct)
    {
        if (filas.Count == 0) return Task.FromResult(0);

        var json = SerializadorFilasJson.Serializar(filas);
        return _sp.ConsultarUnoAsync("INSERTAR",
            p =>
            {
                p.Agregar("@IdFuente", SqlDbType.Int, idFuente);
                p.Agregar("@IdLote", SqlDbType.UniqueIdentifier, idLote);
                p.Agregar("@Datos", SqlDbType.NVarChar, json, -1);
            },
            r => r.GetInt32(r.GetOrdinal("FILAS_AFECTADAS")),
            ct);
    }
}
