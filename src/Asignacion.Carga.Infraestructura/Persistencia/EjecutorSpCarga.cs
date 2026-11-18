using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Asignacion.Carga.Infraestructura.Persistencia;

/// <summary>
/// Único punto que habla con ASIG.SP_CARGA: abre la conexión, manda @Accion
/// y los parámetros, y convierte el resultado. Los repositorios solo describen qué piden.
/// </summary>
internal sealed class EjecutorSpCarga
{
    private readonly OpcionesBaseDatos _opciones;

    public EjecutorSpCarga(IOptions<OpcionesBaseDatos> opciones)
    {
        _opciones = opciones.Value;
        if (string.IsNullOrWhiteSpace(_opciones.CadenaConexion))
            throw new InvalidOperationException($"Falta la cadena de conexión en la sección '{OpcionesBaseDatos.Seccion}:CadenaConexion' del appsettings.");
    }

    public async Task<IReadOnlyList<T>> ConsultarAsync<T>(
        string accion, Action<SqlParameterCollection>? parametros, Func<SqlDataReader, T> mapear, CancellationToken ct)
    {
        await using var conexion = new SqlConnection(_opciones.CadenaConexion);
        await using var comando = CrearComando(conexion, accion, parametros);
        await conexion.OpenAsync(ct);
        await using var lector = await comando.ExecuteReaderAsync(ct);

        var filas = new List<T>();
        while (await lector.ReadAsync(ct))
            filas.Add(mapear(lector));
        return filas;
    }

    public async Task<T> ConsultarUnoAsync<T>(
        string accion, Action<SqlParameterCollection>? parametros, Func<SqlDataReader, T> mapear, CancellationToken ct)
    {
        var filas = await ConsultarAsync(accion, parametros, mapear, ct);
        return filas.Count > 0
            ? filas[0]
            : throw new InvalidOperationException($"{_opciones.Procedimiento} @Accion='{accion}' no devolvió resultado.");
    }

    private SqlCommand CrearComando(SqlConnection conexion, string accion, Action<SqlParameterCollection>? parametros)
    {
        var comando = new SqlCommand(_opciones.Procedimiento, conexion)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = _opciones.TiempoEsperaSegundos
        };
        comando.Parameters.Add("@Accion", SqlDbType.VarChar, 20).Value = accion;
        parametros?.Invoke(comando.Parameters);
        return comando;
    }
}

internal static class SqlExtensiones
{
    public static void Agregar(this SqlParameterCollection p, string nombre, SqlDbType tipo, object? valor, int tamano = 0)
    {
        var parametro = tamano == 0 ? p.Add(nombre, tipo) : p.Add(nombre, tipo, tamano);
        parametro.Value = valor ?? DBNull.Value;
    }

    public static T? Valor<T>(this SqlDataReader r, string columna)
    {
        var i = r.GetOrdinal(columna);
        return r.IsDBNull(i) ? default : r.GetFieldValue<T>(i);
    }

    public static T? ValorNulable<T>(this SqlDataReader r, string columna) where T : struct
    {
        var i = r.GetOrdinal(columna);
        return r.IsDBNull(i) ? null : r.GetFieldValue<T>(i);
    }

    /// <summary>Recorta texto al tamaño de la columna destino.</summary>
    public static string? Recortar(this string? texto, int maximo) =>
        texto is null || texto.Length <= maximo ? texto : texto[..maximo];
}
