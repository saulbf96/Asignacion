using Asignacion.Carga.Dominio.Excepciones;
using Asignacion.Carga.Dominio.Servicios;

namespace Asignacion.Carga.Pruebas.Dominio;

public class ValidadorEncabezadosTests
{
    private static readonly string[] Esperados = ["Actividad", "IDUnico", "id Campania"];

    [Fact]
    public void Sin_faltantes_no_lanza_aunque_cambien_mayusculas_y_espacios()
    {
        var encontrados = new[] { " actividad ", "IDUNICO", "id Campania", "Columna Extra" };

        var faltantes = ValidadorEncabezados.ObtenerFaltantes(Esperados, encontrados);

        Assert.Empty(faltantes);
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
