using System.Runtime.CompilerServices;
using Asignacion.Carga.Aplicacion.CasosUso;
using Asignacion.Carga.Aplicacion.Interfaces;
using Asignacion.Carga.Aplicacion.Notificaciones;
using Asignacion.Carga.Dominio.Entidades;
using Asignacion.Carga.Dominio.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Asignacion.Carga.Pruebas.Aplicacion;

public class ProcesarCargaCasoUsoTests
{
    private static readonly DateOnly Hoy = new(2026, 11, 18);
    private static readonly Guid Lote = Guid.NewGuid();
    private static readonly string[] Esperados = ["Actividad", "IDUnico"];

    private readonly Mock<IRepositorioConfiguracion> _config = new();
    private readonly Mock<IRepositorioBitacora> _bitacora = new();
    private readonly Mock<IRepositorioCarga> _carga = new();
    private readonly Mock<IBuscadorArchivos> _buscador = new();
    private readonly Mock<ILectorArchivo> _lector = new();
    private readonly Mock<INotificador> _notificador = new();
    private readonly List<Notificacion> _enviadas = [];

    private readonly ConfiguracionFuente _fuente =
        new(1, "MOVILIDAD", @"\\175.10.1.1\baz", "BAZv3.2_TEMPRANA_{yyyyMMdd}_*.xlsx", "ASIGNACION", 2, "ASIG.MOVILIDAD", tamanoLote: 2);

    private readonly ArchivoOrigen _archivo =
        new("BAZv3.2_TEMPRANA_20261118_064936.xlsx", @"\\175.10.1.1\baz", Hoy, new DateTime(2026, 11, 18, 6, 49, 0));

    public ProcesarCargaCasoUsoTests()
    {
        _config.Setup(c => c.ObtenerFuentesAsync(It.IsAny<int?>(), It.IsAny<CancellationToken>())).ReturnsAsync([_fuente]);
        _config.Setup(c => c.ObtenerDestinatariosAsync(1, It.IsAny<CancellationToken>()))
               .ReturnsAsync([new Destinatario("wfm_ti@1csb.com.mx"), new Destinatario("desarrollouno@1csb.com.mx")]);
        _config.Setup(c => c.ObtenerEncabezadosEsperadosAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(Esperados);

        _buscador.Setup(b => b.BuscarAsync(_fuente, Hoy, It.IsAny<CancellationToken>())).ReturnsAsync([_archivo]);
        _bitacora.Setup(b => b.IniciarCargaAsync(1, It.IsAny<ArchivoOrigen>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new InicioCarga(Lote, 10, EstatusCarga.EnProceso, 0, null, null));
        _bitacora.Setup(b => b.FinalizarCargaAsync(1, Lote, It.IsAny<EstatusCarga>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((int _, Guid _, EstatusCarga e, int? leidas, string? msg, CancellationToken _) =>
                     new ResultadoCarga(10, _archivo.Nombre, e, leidas, e == EstatusCarga.Exitoso ? leidas : 0, DateTime.Now, DateTime.Now, msg));

        _lector.Setup(l => l.LeerEncabezadosAsync(It.IsAny<ArchivoOrigen>(), "ASIGNACION", 2, It.IsAny<CancellationToken>()))
               .ReturnsAsync(["Actividad", "IDUnico", "folio"]);
        ConFilas(5);

        _carga.Setup(c => c.InsertarLoteAsync(1, Lote, It.IsAny<IReadOnlyList<FilaArchivo>>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((int _, Guid _, IReadOnlyList<FilaArchivo> f, CancellationToken _) => f.Count);

        _notificador.Setup(n => n.EnviarAsync(It.IsAny<Notificacion>(), It.IsAny<IReadOnlyList<Destinatario>>(), It.IsAny<CancellationToken>()))
                    .Callback((Notificacion n, IReadOnlyList<Destinatario> _, CancellationToken _) => _enviadas.Add(n))
                    .Returns(Task.CompletedTask);
    }

    private ProcesarCargaCasoUso Crear()
    {
        var cargador = new CargadorArchivo(_config.Object, _lector.Object, _carga.Object, NullLogger<CargadorArchivo>.Instance);
        var avisador = new AvisadorCarga(_config.Object, _notificador.Object, NullLogger<AvisadorCarga>.Instance);
        var diario = new ProcesadorArchivoDiario(_buscador.Object, _bitacora.Object, cargador, avisador,
            new GeneradorNotificaciones(), NullLogger<ProcesadorArchivoDiario>.Instance);
        return new ProcesarCargaCasoUso(_config.Object, [diario], TimeProvider.System, NullLogger<ProcesarCargaCasoUso>.Instance);
    }

    private Task<IReadOnlyList<ResultadoProceso>> Ejecutar(bool forzar = false) =>
        Crear().EjecutarAsync(new SolicitudCarga(1, Hoy, forzar));

    private void ConFilas(int total) =>
        _lector.Setup(l => l.LeerFilasAsync(It.IsAny<ArchivoOrigen>(), "ASIGNACION", 2, It.IsAny<CancellationToken>()))
               .Returns((ArchivoOrigen _, string _, int _, CancellationToken ct) => Filas(total, ct));

    private static async IAsyncEnumerable<FilaArchivo> Filas(int total, [EnumeratorCancellation] CancellationToken ct = default)
    {
        for (var i = 1; i <= total; i++)
        {
            await Task.Yield();
            yield return new FilaArchivo(i + 2, new Dictionary<string, object?> { ["IDUnico"] = $"ID-{i}" });
        }
    }

    [Fact]
    public async Task Carga_exitosa_inserta_por_lotes_finaliza_y_notifica_inicio_y_fin()
    {
        var r = Assert.Single(await Ejecutar());

        Assert.Equal(EstatusCarga.Exitoso, r.Estatus);
        Assert.Equal(5, r.Detalle!.FilasInsertadas);
        // 5 filas con lotes de 2 → 2 + 2 + 1
        _carga.Verify(c => c.InsertarLoteAsync(1, Lote, It.Is<IReadOnlyList<FilaArchivo>>(f => f.Count == 2), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _carga.Verify(c => c.InsertarLoteAsync(1, Lote, It.Is<IReadOnlyList<FilaArchivo>>(f => f.Count == 1), It.IsAny<CancellationToken>()), Times.Once);
        _bitacora.Verify(b => b.FinalizarCargaAsync(1, Lote, EstatusCarga.Exitoso, 5, null, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal([TipoNotificacion.Inicio, TipoNotificacion.Exitoso], _enviadas.Select(n => n.Tipo));
    }

    [Fact]
    public async Task Sin_archivo_del_dia_registra_evento_y_notifica_sin_cargar()
    {
        _buscador.Setup(b => b.BuscarAsync(_fuente, Hoy, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var r = Assert.Single(await Ejecutar());

        Assert.Equal(EstatusCarga.SinArchivo, r.Estatus);
        _bitacora.Verify(b => b.RegistrarEventoAsync(1, "BAZv3.2_TEMPRANA_20261118_*.xlsx", @"\\175.10.1.1\baz", Hoy,
            EstatusCarga.SinArchivo, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _bitacora.Verify(b => b.IniciarCargaAsync(It.IsAny<int>(), It.IsAny<ArchivoOrigen>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(TipoNotificacion.SinArchivo, Assert.Single(_enviadas).Tipo);
    }

    [Fact]
    public async Task Archivo_ya_cargado_queda_omitido_no_lee_ni_inserta_y_avisa()
    {
        _bitacora.Setup(b => b.IniciarCargaAsync(1, _archivo, false, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new InicioCarga(null, 11, EstatusCarga.Omitido, 0, new DateTime(2026, 11, 18, 7, 0, 0), 50256));

        var r = Assert.Single(await Ejecutar());

        Assert.Equal(EstatusCarga.Omitido, r.Estatus);
        _lector.Verify(l => l.LeerFilasAsync(It.IsAny<ArchivoOrigen>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _carga.VerifyNoOtherCalls();
        var aviso = Assert.Single(_enviadas);
        Assert.Equal(TipoNotificacion.Omitido, aviso.Tipo);
        Assert.Contains("no se cargó nada", aviso.Cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("50,256", aviso.Cuerpo);
    }

    [Fact]
    public async Task Forzar_se_pasa_a_la_bitacora()
    {
        await Ejecutar(forzar: true);

        _bitacora.Verify(b => b.IniciarCargaAsync(1, _archivo, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Si_falla_la_insercion_finaliza_como_error_y_notifica()
    {
        _carga.Setup(c => c.InsertarLoteAsync(1, Lote, It.IsAny<IReadOnlyList<FilaArchivo>>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException("timeout de SQL"));

        var r = Assert.Single(await Ejecutar());

        Assert.True(r.FueError);
        Assert.Equal("timeout de SQL", r.Mensaje);
        _bitacora.Verify(b => b.FinalizarCargaAsync(1, Lote, EstatusCarga.Error, null, "timeout de SQL", It.IsAny<CancellationToken>()), Times.Once);
        _bitacora.Verify(b => b.FinalizarCargaAsync(1, Lote, EstatusCarga.Exitoso, It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal([TipoNotificacion.Inicio, TipoNotificacion.Error], _enviadas.Select(n => n.Tipo));
    }

    [Fact]
    public async Task Si_faltan_encabezados_no_inserta_nada_y_queda_en_error()
    {
        _lector.Setup(l => l.LeerEncabezadosAsync(It.IsAny<ArchivoOrigen>(), "ASIGNACION", 2, It.IsAny<CancellationToken>()))
               .ReturnsAsync(["Actividad", "OTRA"]);

        var r = Assert.Single(await Ejecutar());

        Assert.True(r.FueError);
        Assert.Contains("IDUnico", r.Mensaje);
        _carga.VerifyNoOtherCalls();
        _bitacora.Verify(b => b.FinalizarCargaAsync(1, Lote, EstatusCarga.Error, null, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Si_no_se_puede_acceder_a_la_ruta_registra_evento_de_error()
    {
        _buscador.Setup(b => b.BuscarAsync(_fuente, Hoy, It.IsAny<CancellationToken>()))
                 .ThrowsAsync(new IOException("No se encuentra la ruta de red"));

        var r = Assert.Single(await Ejecutar());

        Assert.True(r.FueError);
        _bitacora.Verify(b => b.RegistrarEventoAsync(1, It.IsAny<string>(), It.IsAny<string?>(), Hoy,
            EstatusCarga.Error, "No se encuentra la ruta de red", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(TipoNotificacion.Error, Assert.Single(_enviadas).Tipo);
    }

    [Fact]
    public async Task Si_el_correo_falla_la_carga_sigue_siendo_exitosa()
    {
        _notificador.Setup(n => n.EnviarAsync(It.IsAny<Notificacion>(), It.IsAny<IReadOnlyList<Destinatario>>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new InvalidOperationException("SMTP caído"));

        var r = Assert.Single(await Ejecutar());

        Assert.Equal(EstatusCarga.Exitoso, r.Estatus);
    }

    [Fact]
    public async Task Con_varios_archivos_del_dia_toma_el_mas_reciente_y_lo_avisa()
    {
        var anterior = _archivo with { Nombre = "BAZv3.2_TEMPRANA_20261118_050000.xlsx", FechaModificacion = _archivo.FechaModificacion.AddHours(-2) };
        _buscador.Setup(b => b.BuscarAsync(_fuente, Hoy, It.IsAny<CancellationToken>())).ReturnsAsync([anterior, _archivo]);

        await Ejecutar();

        _bitacora.Verify(b => b.IniciarCargaAsync(1, _archivo, false, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains("se tomó el más reciente", _enviadas.First(n => n.Tipo == TipoNotificacion.Inicio).Cuerpo);
    }

    [Fact]
    public async Task Sin_correos_configurados_no_intenta_enviar()
    {
        _config.Setup(c => c.ObtenerDestinatariosAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var r = Assert.Single(await Ejecutar());

        Assert.Equal(EstatusCarga.Exitoso, r.Estatus);
        _notificador.VerifyNoOtherCalls();
    }
}
