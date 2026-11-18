using Asignacion.Carga.Dominio.Entidades;
using Asignacion.Carga.Dominio.Excepciones;

namespace Asignacion.Carga.Pruebas.Dominio;

public class ConfiguracionFuenteTests
{
    private static ConfiguracionFuente Crear(
        string patron = "BAZv3.2_TEMPRANA_{yyyyMMdd}_*.xlsx", int fila = 2, int lote = 5000, string ruta = @"\\175.10.1.1\baz") =>
        new(1, "MOVILIDAD", ruta, patron, "ASIGNACION", fila, "ASIG.MOVILIDAD", lote);

    [Fact]
    public void ResolverPatron_reemplaza_la_fecha_y_conserva_el_comodin()
    {
        var config = Crear();

        var patron = config.ResolverPatron(new DateOnly(2026, 11, 18));

        Assert.Equal("BAZv3.2_TEMPRANA_20261118_*.xlsx", patron);
    }

    [Fact]
    public void ResolverPatron_admite_varios_marcadores()
    {
        var config = Crear(patron: "{yyyy}/{MM}/ARCHIVO_{ddMMyyyy}.xlsx");

        Assert.Equal("2026/01/ARCHIVO_05012026.xlsx", config.ResolverPatron(new DateOnly(2026, 1, 5)));
    }

    [Fact]
    public void ResolverPatron_sin_marcadores_devuelve_el_patron_igual()
    {
        var config = Crear(patron: "FIJO.xlsx");

        Assert.Equal("FIJO.xlsx", config.ResolverPatron(new DateOnly(2026, 11, 18)));
    }

    [Theory]
    [InlineData(0, 5000)]
    [InlineData(2, 0)]
    public void Valores_fuera_de_rango_lanzan_excepcion(int fila, int lote)
    {
        Assert.Throws<ConfiguracionInvalidaException>(() => Crear(fila: fila, lote: lote));
    }

    [Fact]
    public void Campo_obligatorio_vacio_lanza_excepcion()
    {
        var ex = Assert.Throws<ConfiguracionInvalidaException>(() => Crear(ruta: "  "));

        Assert.Contains("RutaOrigen", ex.Message);
    }
}
