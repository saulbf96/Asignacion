using System.Runtime.CompilerServices;
using Asignacion.Carga.Aplicacion.CasosUso;
using Asignacion.Carga.Aplicacion.Interfaces;
using Asignacion.Carga.Aplicacion.Notificaciones;
using Asignacion.Carga.Dominio.Entidades;
using Asignacion.Carga.Dominio.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Asignacion.Carga.Pruebas.Aplicacion;

public class ProcesadorUnificadoTests
{
    private static readonly DateOnly Hoy = new(2026, 11, 18);
    private static readonly Guid Lote = Guid.NewGuid();

    private readonly Mock<IRepositorioConfiguracion> _config = new();
    private readonly Mock<IRepositorioBitacora> _bitacora = new();
    private readonly Mock<IRepositorioCarga> _carga = new();
    private readonly Mock<ILectorArchivo> _lector = new();
    private readonly Mock<IUnificadorArchivos> _unificador = new(MockBehavior.Strict);
    private readonly Mock<INotificador> _notificador = new();
    private readonly List<string> _pasos = [];
    private readonly List<Notificacion> _correos = [];

    private readonly ConfiguracionFuente _fuente = new(
        2, "LEGAL", @"\\srv\ASIGNACIÓN_", "*.xlsx", "CARTERA", 1, "ASIG.LEGAL", 2,
        TipoProceso.Unificar, "|", @"\\srv\CARTERAS\{MES}", "Cartera_ISB_{ddMMyyyy}.xlsx", "Cartera", null, borrarEntrada: true);

    private readonly ArchivoUnificado _unificado = new(
        new ArchivoOrigen("Cartera_ISB_18112026.xlsx", @"C:\temp\x", Hoy, DateTime.Now),
        new ArchivoOrigen("Cartera_ISB_18112026.xlsx", @"\\srv\CARTERAS\NOVIEMBRE", Hoy, DateTime.Now),
        "Cartera", [@"\\srv\ASIGNACIÓN_\legal 18.zip", @"\\srv\ASIGNACIÓN_\legal 18"], 13, 13, 3, 0, 0, new Dictionary<string, int>(), @"C:\temp\x");

    public ProcesadorUnificadoTests()
    {
        _config.Setup(c => c.ObtenerDestinatariosAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync([new Destinatario("wfm_ti@1csb.com.mx")]);
        _config.Setup(c => c.ObtenerEncabezadosEsperadosAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(["CLIENTE_UNICO"]);

        _unificador.Setup(u => u.UnificarAsync(_fuente, Hoy, It.IsAny<CancellationToken>()))
                   .Callback(() => _pasos.Add("unificar")).ReturnsAsync(_unificado);
        _unificador.Setup(u => u.PublicarAsync(_unificado, It.IsAny<CancellationToken>()))
                   .Callback(() => _pasos.Add("publicar")).Returns(Task.CompletedTask);
        _unificador.Setup(u => u.VaciarEntradaAsync(_unificado, It.IsAny<CancellationToken>()))
                   .Callback(() => _pasos.Add("vaciar")).ReturnsAsync([]);
        _unificador.Setup(u => u.DescartarTemporalesAsync(_unificado))
                   .Callback(() => _pasos.Add("descartar")).Returns(Task.CompletedTask);

        _bitacora.Setup(b => b.IniciarCargaAsync(2, _unificado.Destino, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                 .Callback(() => _pasos.Add("iniciar"))
                 .ReturnsAsync(new InicioCarga(Lote, 20, EstatusCarga.EnProceso, 0, null, null));
        _bitacora.Setup(b => b.FinalizarCargaAsync(2, Lote, It.IsAny<EstatusCarga>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                 .Callback((int _, Guid _, EstatusCarga e, int? _, string? _, CancellationToken _) => _pasos.Add($"finalizar:{e}"))
                 .ReturnsAsync((int _, Guid _, EstatusCarga e, int? l, string? m, CancellationToken _) =>
                     new ResultadoCarga(20, _unificado.Destino.Nombre, e, l, e == EstatusCarga.Exitoso ? l : 0, DateTime.Now, DateTime.Now, m));

        _lector.Setup(l => l.LeerEncabezadosAsync(_unificado.Lectura, "Cartera", 1, It.IsAny<CancellationToken>())).ReturnsAsync(["CLIENTE_UNICO"]);
        _lector.Setup(l => l.LeerFilasAsync(_unificado.Lectura, "Cartera", 1, It.IsAny<CancellationToken>()))
               .Returns((ArchivoOrigen _, string _, int _, CancellationToken ct) => Filas(3, ct));
        _carga.Setup(c => c.InsertarLoteAsync(2, Lote, It.IsAny<IReadOnlyList<FilaArchivo>>(), It.IsAny<CancellationToken>()))
              .Callback(() => _pasos.Add("insertar"))
              .ReturnsAsync((int _, Guid _, IReadOnlyList<FilaArchivo> f, CancellationToken _) => f.Count);

        _notificador.Setup(n => n.EnviarAsync(It.IsAny<Notificacion>(), It.IsAny<IReadOnlyList<Destinatario>>(), It.IsAny<CancellationToken>()))
                    .Callback((Notificacion n, IReadOnlyList<Destinatario> _, CancellationToken _) => _correos.Add(n))
                    .Returns(Task.CompletedTask);
    }

    private static async IAsyncEnumerable<FilaArchivo> Filas(int total, [EnumeratorCancellation] CancellationToken ct = default)
    {
        for (var i = 1; i <= total; i++)
        {
            await Task.Yield();
            yield return new FilaArchivo(i + 1, new Dictionary<string, object?> { ["CLIENTE_UNICO"] = $"1-1-{i}" });
        }
    }

    private Task<ResultadoProceso> Procesar()
    {
        var cargador = new CargadorArchivo(_config.Object, _lector.Object, _carga.Object, NullLogger<CargadorArchivo>.Instance);
        var avisador = new AvisadorCarga(_config.Object, _notificador.Object, NullLogger<AvisadorCarga>.Instance);
        var p = new ProcesadorUnificado(_unificador.Object, _bitacora.Object, cargador, avisador,
            new GeneradorNotificaciones(), NullLogger<ProcesadorUnificado>.Instance);
        return p.ProcesarAsync(_fuente, Hoy, forzar: false, CancellationToken.None);
    }

    [Fact]
    public async Task Orden_correcto_unifica_inserta_publica_finaliza_y_vacia_la_entrada()
    {
        var r = await Procesar();

        Assert.Equal(EstatusCarga.Exitoso, r.Estatus);
        Assert.Equal("Cartera_ISB_18112026.xlsx", r.Archivo);
        // lotes de 2: 3 filas = 2 inserciones
        Assert.Equal(["unificar", "iniciar", "insertar", "insertar", "publicar", "finalizar:Exitoso", "vaciar", "descartar"], _pasos);
        Assert.Equal([TipoNotificacion.Inicio, TipoNotificacion.Exitoso], _correos.Select(c => c.Tipo));
        Assert.Contains(@"\\srv\CARTERAS\NOVIEMBRE\Cartera_ISB_18112026.xlsx", _correos[1].Cuerpo);
        Assert.Contains("13 archivo(s) venían repetidos", _correos[0].Cuerpo);
    }

    [Fact]
    public async Task Siempre_recarga_porque_si_hay_entrada_son_datos_nuevos()
    {
        await Procesar();

        _bitacora.Verify(b => b.IniciarCargaAsync(2, _unificado.Destino, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Carpeta_de_entrada_vacia_es_sin_archivo()
    {
        _unificador.Setup(u => u.UnificarAsync(_fuente, Hoy, It.IsAny<CancellationToken>())).ReturnsAsync((ArchivoUnificado?)null);

        var r = await Procesar();

        Assert.Equal(EstatusCarga.SinArchivo, r.Estatus);
        Assert.Equal(TipoNotificacion.SinArchivo, Assert.Single(_correos).Tipo);
        _bitacora.Verify(b => b.RegistrarEventoAsync(2, _fuente.RutaOrigen, _fuente.RutaOrigen, Hoy, EstatusCarga.SinArchivo,
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Si_no_se_puede_guardar_el_unificado_queda_en_error_y_NO_se_borra_la_entrada()
    {
        _unificador.Setup(u => u.PublicarAsync(_unificado, It.IsAny<CancellationToken>()))
                   .Callback(() => _pasos.Add("publicar")).ThrowsAsync(new IOException("archivo abierto en Excel"));

        var r = await Procesar();

        Assert.True(r.FueError);
        Assert.Contains("finalizar:Error", _pasos);
        Assert.DoesNotContain("vaciar", _pasos);
        Assert.Equal("descartar", _pasos[^1]);
        Assert.Equal(TipoNotificacion.Error, _correos[^1].Tipo);
    }

    [Fact]
    public async Task Si_falla_la_unificacion_no_se_inicia_ni_se_borra_nada()
    {
        _unificador.Setup(u => u.UnificarAsync(_fuente, Hoy, It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new InvalidOperationException("contraseña incorrecta"));

        var r = await Procesar();

        Assert.True(r.FueError);
        Assert.Equal("contraseña incorrecta", r.Mensaje);
        _bitacora.Verify(b => b.IniciarCargaAsync(It.IsAny<int>(), It.IsAny<ArchivoOrigen>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        _bitacora.Verify(b => b.RegistrarEventoAsync(2, "Cartera_ISB_18112026.xlsx", It.IsAny<string?>(), Hoy, EstatusCarga.Error,
            "contraseña incorrecta", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Si_algo_de_la_entrada_no_se_puede_borrar_la_carga_sigue_exitosa_y_se_avisa()
    {
        _unificador.Setup(u => u.VaciarEntradaAsync(_unificado, It.IsAny<CancellationToken>())).ReturnsAsync([@"\\srv\ASIGNACIÓN_\legal 18"]);

        var r = await Procesar();

        Assert.Equal(EstatusCarga.Exitoso, r.Estatus);
        Assert.Contains("legal 18", r.Mensaje);
        Assert.Contains("revisar carpeta de entrada", _correos[^1].Asunto);
    }
}
