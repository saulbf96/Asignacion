using System.Text.Json;
using Asignacion.Carga.Dominio.Entidades;
using Asignacion.Carga.Infraestructura.Persistencia;

namespace Asignacion.Carga.Pruebas.Infraestructura;

public class SerializadorFilasJsonTests
{
    [Theory]
    [InlineData(19764.0, "19764")]
    [InlineData(899.07, "899.07")]
    [InlineData(1E+15, "1000000000000000")]
    [InlineData(0.0, "0")]
    public void Numeros_sin_notacion_cientifica_y_con_punto(double valor, string esperado)
    {
        Assert.Equal(esperado, SerializadorFilasJson.ATexto(valor));
    }

    [Fact]
    public void Fechas_en_formato_iso_y_vacios_como_null()
    {
        Assert.Equal("2026-11-24", SerializadorFilasJson.ATexto(new DateTime(2026, 11, 24)));
        Assert.Equal("2026-11-24 07:30:00", SerializadorFilasJson.ATexto(new DateTime(2026, 11, 24, 7, 30, 0)));
        Assert.Null(SerializadorFilasJson.ATexto("   "));
        Assert.Null(SerializadorFilasJson.ATexto(null));
        Assert.Equal("1", SerializadorFilasJson.ATexto(true));
    }

    [Fact]
    public void Genera_arreglo_con_encabezados_exactos_y_omite_celdas_vacias()
    {
        var filas = new[]
        {
            new FilaArchivo(3, new Dictionary<string, object?>
            {
                ["id Campania"] = "2723", ["saldo"] = 19764.0, ["nombre"] = "NUÑEZ \"EL\" PÉREZ", ["hora"] = null
            })
        };

        var json = SerializadorFilasJson.Serializar(filas);

        using var doc = JsonDocument.Parse(json);
        var fila = Assert.Single(doc.RootElement.EnumerateArray().ToList());
        Assert.Equal("2723", fila.GetProperty("id Campania").GetString());
        Assert.Equal("19764", fila.GetProperty("saldo").GetString());
        Assert.Equal("NUÑEZ \"EL\" PÉREZ", fila.GetProperty("nombre").GetString());
        Assert.False(fila.TryGetProperty("hora", out _));
    }
}
