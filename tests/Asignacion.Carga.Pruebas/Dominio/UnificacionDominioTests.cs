using Asignacion.Carga.Dominio.Entidades;
using Asignacion.Carga.Dominio.Enums;
using Asignacion.Carga.Dominio.Excepciones;
using Asignacion.Carga.Dominio.Servicios;

namespace Asignacion.Carga.Pruebas.Dominio;

public class UnificacionDominioTests
{
    private static ConfiguracionFuente Legal(string? separador = "|", string? rutaSalida = @"\\srv\CARTERAS\{MES}", string? correcciones = "TIPO_GESTION_>GESTORES") =>
        new(2, "LEGAL", @"\\srv\CARTERAS\ASIGNACIÓN_", "*.xlsx", "CARTERA", 1, "ASIG.LEGAL", 5000,
            TipoProceso.Unificar, separador, rutaSalida, "Cartera_ISB_{ddMMyyyy}.xlsx", "Cartera", correcciones, borrarEntrada: true);

    [Theory]
    [InlineData(2026, 11, 18, "NOVIEMBRE", "Cartera_ISB_18112026.xlsx")]
    [InlineData(2026, 12, 1, "DICIEMBRE", "Cartera_ISB_01122026.xlsx")]
    [InlineData(2027, 9, 5, "SEPTIEMBRE", "Cartera_ISB_05092027.xlsx")]
    public void Resuelve_carpeta_del_mes_y_nombre_con_la_nomenclatura(int a, int m, int d, string mes, string nombre)
    {
        var f = Legal();
        var fecha = new DateOnly(a, m, d);

        Assert.Equal($@"\\srv\CARTERAS\{mes}", f.ResolverRutaSalida(fecha));
        Assert.Equal(nombre, f.ResolverNombreSalida(fecha));
    }

    [Fact]
    public void Unificar_exige_separador_y_ruta_de_salida()
    {
        Assert.Throws<ConfiguracionInvalidaException>(() => Legal(separador: null));
        Assert.Throws<ConfiguracionInvalidaException>(() => Legal(rutaSalida: null));
    }

    [Fact]
    public void Lee_correcciones_y_rechaza_las_mal_escritas()
    {
        Assert.Equal([new CorreccionColumna("A", "B"), new CorreccionColumna("C", "D")], CorreccionColumna.Leer(" A>B ; C>D "));
        Assert.Empty(CorreccionColumna.Leer(null));
        Assert.Throws<ConfiguracionInvalidaException>(() => CorreccionColumna.Leer("A-B"));
        Assert.Throws<ConfiguracionInvalidaException>(() => CorreccionColumna.Leer("A>A"));
    }

    [Fact]
    public void Corrector_mueve_TIPO_GESTION_a_GESTORES_solo_si_GESTORES_esta_vacio()
    {
        string[] encabezados = ["CLIENTE_UNICO", "GESTORES", "TIPO_GESTION_"];
        var corrector = new CorrectorColumnas(encabezados, [new CorreccionColumna("TIPO_GESTION_", "GESTORES")]);

        var corrida = new string?[] { "1-1", null, "MAXIMUS" };
        var bien = new string?[] { "1-2", "BANCO AZTECA", null };
        var ambos = new string?[] { "1-3", "HIBRIDO", "MAXIMUS" };

        Assert.Equal(1, corrector.Aplicar(corrida));
        Assert.Equal(new string?[] { "1-1", "MAXIMUS", null }, corrida);
        Assert.Equal(0, corrector.Aplicar(bien));
        Assert.Equal(0, corrector.Aplicar(ambos)); // no se pisa un dato que ya existe
        Assert.Equal("HIBRIDO", ambos[1]);
    }

    [Fact]
    public void Corrector_ignora_correcciones_de_columnas_que_no_existen()
    {
        var corrector = new CorrectorColumnas(["CLIENTE_UNICO", "GESTORES"], [new CorreccionColumna("TIPO_GESTION_", "GESTORES")]);

        Assert.False(corrector.TieneCorrecciones);
    }
}
