using Asignacion.Carga.Dominio.Entidades;
using Asignacion.Carga.Dominio.Enums;
using Asignacion.Carga.Dominio.Excepciones;

namespace Asignacion.Carga.Pruebas.Dominio;

public class EntidadesTests
{
    [Theory]
    [InlineData("~$BAZv3.2_TEMPRANA_20261118_064936.xlsx", true)]
    [InlineData(@"W:\~$BAZv3.2_TEMPRANA_20261118_064936.xlsx", true)]
    [InlineData("BAZv3.2_TEMPRANA_20261118_064936.xlsx", false)]
    public void Detecta_archivos_temporales_de_Office(string nombre, bool esperado)
    {
        Assert.Equal(esperado, ArchivoOrigen.EsTemporalDeOffice(nombre));
    }

    [Theory]
    [InlineData("wfm_ti@1csb.com.mx")]
    [InlineData("  desarrollouno@1csb.com.mx ")]
    public void Destinatario_valido(string correo)
    {
        var d = new Destinatario(correo, " WFM ");

        Assert.Equal(correo.Trim(), d.Correo);
        Assert.Equal("WFM", d.Nombre);
    }

    [Theory]
    [InlineData("")]
    [InlineData("sin-arroba")]
    [InlineData("Nombre <correo@1csb.com.mx>")]
    public void Destinatario_invalido_lanza_excepcion(string correo)
    {
        Assert.Throws<ConfiguracionInvalidaException>(() => new Destinatario(correo));
    }

    [Fact]
    public void ResultadoCarga_calcula_duracion_y_si_esta_completa()
    {
        var inicio = new DateTime(2026, 11, 18, 7, 0, 0);
        var r = new ResultadoCarga(1, "a.xlsx", EstatusCarga.Exitoso, 50256, 50256, inicio, inicio.AddMinutes(2), null);

        Assert.Equal(TimeSpan.FromMinutes(2), r.Duracion);
        Assert.True(r.EstaCompleta);
    }

    [Fact]
    public void InicioCarga_omitida()
    {
        var i = new InicioCarga(null, 2, EstatusCarga.Omitido, 0, DateTime.Now, 50256);

        Assert.True(i.FueOmitida);
    }
}
