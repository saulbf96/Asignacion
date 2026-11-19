using Asignacion.Carga.Dominio.Entidades;
using Asignacion.Carga.Dominio.Servicios;

namespace Asignacion.Carga.Pruebas.Dominio;

/// <summary>Mismos resultados que el "Tipo cambiado" + "Errores reemplazados" del Power Query de WFM (verificados contra Cartera_ISB_19112026).</summary>
public class ConvertidorTiposTests
{
    [Theory]
    [InlineData("09700", "9700")]          // CP_CTE: pierde el cero (como Power Query)
    [InlineData("S/N", "0")]               // NUM_EXT_CTE: texto → 0
    [InlineData("SIN TELEFONO", "0")]
    [InlineData("N/A", "0")]
    [InlineData("5032.96", "5033")]        // PAGO_NORMAL: Int64 redondea
    [InlineData("2.5", "2")]               // a par (Power Query redondea al par)
    [InlineData("127972010156293225", "127972010156293225")] // CLAVE_SPEI: entero exacto, 18 dígitos
    [InlineData(" 12 ", "12")]
    [InlineData("NULL", null)]             // casos reales del 19/11, mismo resultado que Power Query
    [InlineData("202 -", "-202")]
    [InlineData("6-", "-6")]
    [InlineData("2,3", "23")]
    [InlineData("666,.", "666")]
    [InlineData("7e2", "700")]
    [InlineData("50E16", "500000000000000000")]
    [InlineData("4.5", "4")]
    [InlineData("7.5", "8")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Entero(string? valor, string? esperado) => Assert.Equal(esperado, ConvertidorTipos.Convertir(valor, TipoColumna.Entero));

    [Theory]
    [InlineData(".01", "0.01")]
    [InlineData("19771.50", "19771.5")]
    [InlineData("-106.40616", "-106.40616")]
    [InlineData("N/A", "0")]
    public void Decimal(string valor, string esperado) => Assert.Equal(esperado, ConvertidorTipos.Convertir(valor, TipoColumna.Decimal));

    [Theory]
    [InlineData("19/11/2026", "19/11/2026")]
    [InlineData("N/A", null)]
    [InlineData("01/01/0001", null)]       // Excel no la representa: vacío, como en el manual
    [InlineData("31/02/2026", null)]
    public void Fecha(string valor, string? esperado) => Assert.Equal(esperado, ConvertidorTipos.Convertir(valor, TipoColumna.Fecha));

    [Theory]
    [InlineData("1")]
    [InlineData("NULL")]
    [InlineData("0005")]
    public void Texto_no_cambia(string valor) => Assert.Equal(valor, ConvertidorTipos.Convertir(valor, TipoColumna.Texto));
}
