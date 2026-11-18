using Asignacion.Carga.Aplicacion.CasosUso;
using Asignacion.Carga.Consola;
using Asignacion.Carga.Dominio.Enums;

namespace Asignacion.Carga.Pruebas.Consola;

public class ArgumentosConsolaTests
{
    [Fact]
    public void Sin_parametros_procesa_todas_las_fuentes_de_hoy_sin_forzar()
    {
        Assert.True(ArgumentosConsola.TryParse([], out var a, out _));

        Assert.Equal(new SolicitudCarga(), a.Solicitud);
        Assert.False(a.MostrarAyuda);
    }

    [Fact]
    public void Lee_todos_los_parametros()
    {
        Assert.True(ArgumentosConsola.TryParse(["--fuente", "1", "--FECHA", "2026-11-18", "--forzar", "--ambiente", "Pruebas"], out var a, out _));

        Assert.Equal(new SolicitudCarga(1, new DateOnly(2026, 11, 18), true), a.Solicitud);
        Assert.Equal("Pruebas", a.Ambiente);
    }

    [Theory]
    [InlineData("--fuente")]
    [InlineData("--fuente", "cero")]
    [InlineData("--fuente", "0")]
    [InlineData("--fecha", "18/11/2026")]
    [InlineData("--fecha", "--forzar")]
    [InlineData("--desconocido")]
    public void Parametros_invalidos(params string[] args)
    {
        Assert.False(ArgumentosConsola.TryParse(args, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Ayuda()
    {
        Assert.True(ArgumentosConsola.TryParse(["--ayuda"], out var a, out _));
        Assert.True(a.MostrarAyuda);
    }
}

public class CodigoSalidaTests
{
    private static ResultadoProceso R(EstatusCarga e) => new(1, "MOVILIDAD", e, null, null, null);

    [Theory]
    [InlineData(new[] { EstatusCarga.Exitoso }, CodigoSalida.Correcto)]
    [InlineData(new[] { EstatusCarga.Omitido }, CodigoSalida.Correcto)]
    [InlineData(new[] { EstatusCarga.SinArchivo }, CodigoSalida.SinArchivo)]
    [InlineData(new[] { EstatusCarga.Exitoso, EstatusCarga.Error }, CodigoSalida.Error)]
    [InlineData(new[] { EstatusCarga.SinArchivo, EstatusCarga.Error }, CodigoSalida.Error)]
    [InlineData(new EstatusCarga[0], CodigoSalida.Correcto)]
    public void Error_gana_luego_sin_archivo(EstatusCarga[] estatus, int esperado)
    {
        Assert.Equal(esperado, CodigoSalida.Desde(estatus.Select(R).ToList()));
    }
}
