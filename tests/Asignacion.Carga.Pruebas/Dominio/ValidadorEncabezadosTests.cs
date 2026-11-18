using Asignacion.Carga.Dominio.Excepciones;
using Asignacion.Carga.Dominio.Servicios;

namespace Asignacion.Carga.Pruebas.Dominio;

public class ValidadorEncabezadosTests
{
    private static readonly string[] Esperados = ["Actividad", "IDUnico", "id Campania"];

    [Fact]
    public void Sin_faltantes_ignora_espacios_y_columnas_extra()
    {
        var encontrados = new[] { " Actividad ", "IDUnico", "id Campania", "Columna Extra" };

        var faltantes = ValidadorEncabezados.ObtenerFaltantes(Esperados, encontrados);

        Assert.Empty(faltantes);
    }

    [Fact]
    public void Distingue_mayusculas_porque_el_SP_lee_el_JSON_por_nombre_exacto()
    {
        var encontrados = new[] { "Actividad", "IDUNICO", "id Campania" };

        Assert.Equal(["IDUnico"], ValidadorEncabezados.ObtenerFaltantes(Esperados, encontrados));
    }

    [Fact]
    public void Reporta_los_encabezados_que_faltan()
    {
        var encontrados = new string?[] { "Actividad", null, "" };

        var ex = Assert.Throws<EncabezadosInvalidosException>(
            () => ValidadorEncabezados.Validar("archivo.xlsx", Esperados, encontrados));

        Assert.Equal(["IDUnico", "id Campania"], ex.Faltantes);
        Assert.Contains("archivo.xlsx", ex.Message);
    }
}
