using System.Diagnostics;
using Asignacion.Carga.Aplicacion;
using Asignacion.Carga.Aplicacion.CasosUso;
using Asignacion.Carga.Aplicacion.Interfaces;
using Asignacion.Carga.Aplicacion.Notificaciones;
using Asignacion.Carga.Dominio.Entidades;
using Asignacion.Carga.Dominio.Enums;
using Asignacion.Carga.Infraestructura;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Asignacion.Carga.Pruebas.Integracion;

/// <summary>
/// Carga de punta a punta contra SQL Server con el archivo real (solo el correo es simulado).
/// Requiere la variable de entorno ASIGNACION_SQL_PRUEBAS con la cadena de conexión a una base
/// con los scripts aplicados, y que CONFIG_FUENTE apunte a la carpeta del archivo de ejemplo.
/// Sin la variable, la prueba no hace nada.
/// </summary>
public class CargaCompletaSqlTests
{
    private static readonly DateOnly Fecha = new(2026, 11, 18);
    private const string Archivo = "BAZv3.2_TEMPRANA_20261118_064936.xlsx";
    private readonly ITestOutputHelper _salida;

    public CargaCompletaSqlTests(ITestOutputHelper salida) => _salida = salida;

    [Fact]
    public async Task Carga_real_luego_omitida_luego_forzada()
    {
        var cadena = Environment.GetEnvironmentVariable("ASIGNACION_SQL_PRUEBAS");
        if (string.IsNullOrWhiteSpace(cadena))
        {
            _salida.WriteLine("Se omite: no está definida ASIGNACION_SQL_PRUEBAS.");
            return;
        }

        // Candado: esta prueba BORRA ASIG.MOVILIDAD; nunca debe correr fuera de una base local
        var baseDatos = new SqlConnectionStringBuilder(cadena).InitialCatalog;
        Assert.True(baseDatos.EndsWith("_LOCAL", StringComparison.OrdinalIgnoreCase),
            $"Por seguridad esta prueba solo corre en una base *_LOCAL (se indicó '{baseDatos}').");

        await LimpiarAsync(cadena);
        var correos = new CorreosCapturados();
        await using var proveedor = CrearServicios(cadena, correos);
        var casoUso = proveedor.GetRequiredService<ProcesarCargaCasoUso>();

        // 1. Primera carga: completa
        var reloj = Stopwatch.StartNew();
        var r1 = Assert.Single(await casoUso.EjecutarAsync(new SolicitudCarga(1, Fecha)));
        _salida.WriteLine($"Carga completa en {reloj.Elapsed.TotalSeconds:N1} s: {r1.Detalle?.FilasInsertadas} filas. {r1.Mensaje}");

        Assert.True(r1.Estatus == EstatusCarga.Exitoso, r1.Mensaje);
        Assert.Equal(Archivo, r1.Archivo);
        Assert.Equal(50256, r1.Detalle!.FilasLeidas);
        Assert.Equal(50256, r1.Detalle.FilasInsertadas);
        Assert.Equal([TipoNotificacion.Inicio, TipoNotificacion.Exitoso], correos.Tipos);
        Assert.Equal(50256, await ContarAsync(cadena, "SELECT COUNT(*) FROM ASIG.MOVILIDAD"));
        Assert.Equal(0, await ContarAsync(cadena,
            "SELECT COUNT(*) FROM ASIG.MOVILIDAD WHERE ID_UNICO IS NULL OR SALDO IS NULL OR FECHA_VENCIMIENTO IS NULL OR ID_CLIENTE IS NULL OR TELEFONO_1 IS NULL"));

        // 2. Se vuelve a ejecutar: no carga nada
        correos.Tipos.Clear();
        var r2 = Assert.Single(await casoUso.EjecutarAsync(new SolicitudCarga(1, Fecha)));
        Assert.Equal(EstatusCarga.Omitido, r2.Estatus);
        Assert.Equal([TipoNotificacion.Omitido], correos.Tipos);
        Assert.Equal(50256, await ContarAsync(cadena, "SELECT COUNT(*) FROM ASIG.MOVILIDAD"));

        // 3. Forzada: recarga sin duplicar
        var r3 = Assert.Single(await casoUso.EjecutarAsync(new SolicitudCarga(1, Fecha, Forzar: true)));
        Assert.Equal(EstatusCarga.Exitoso, r3.Estatus);
        Assert.Equal(50256, await ContarAsync(cadena, "SELECT COUNT(*) FROM ASIG.MOVILIDAD"));
        Assert.Equal(1, await ContarAsync(cadena,
            $"SELECT COUNT(*) FROM ASIG.BITACORA_CARGA WHERE ARCHIVO_ORIGEN = '{Archivo}' AND ESTATUS = 'REEMPLAZADO'"));

        // 4. Día sin archivo
        var r4 = Assert.Single(await casoUso.EjecutarAsync(new SolicitudCarga(1, Fecha.AddDays(1))));
        Assert.Equal(EstatusCarga.SinArchivo, r4.Estatus);

        await LimpiarAsync(cadena);
    }

    private static ServiceProvider CrearServicios(string cadena, CorreosCapturados correos)
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["BaseDatos:CadenaConexion"] = cadena })
            .Build();

        var services = new ServiceCollection()
            .AddLogging()
            .AgregarAplicacion()
            .AgregarInfraestructura(configuracion);
        services.AddSingleton<INotificador>(correos); // el correo real no se prueba aquí
        return services.BuildServiceProvider();
    }

    private static async Task LimpiarAsync(string cadena)
    {
        await using var c = new SqlConnection(cadena);
        await c.OpenAsync();
        await using var cmd = new SqlCommand(
            "DELETE FROM ASIG.MOVILIDAD; DELETE FROM ASIG.BITACORA_CARGA WHERE ARCHIVO_ORIGEN LIKE 'BAZv3.2_TEMPRANA_202611%';", c);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<int> ContarAsync(string cadena, string sql)
    {
        await using var c = new SqlConnection(cadena);
        await c.OpenAsync();
        await using var cmd = new SqlCommand(sql, c);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private sealed class CorreosCapturados : INotificador
    {
        public List<TipoNotificacion> Tipos { get; } = [];

        public Task EnviarAsync(Notificacion notificacion, IReadOnlyList<Destinatario> destinatarios, CancellationToken ct)
        {
            Tipos.Add(notificacion.Tipo);
            return Task.CompletedTask;
        }
    }
}
