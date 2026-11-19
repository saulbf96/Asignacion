using Asignacion.Carga.Dominio.Entidades;
using Asignacion.Carga.Dominio.Enums;
using Asignacion.Carga.Dominio.Excepciones;
using Asignacion.Carga.Infraestructura.Archivos;
using Asignacion.Carga.Infraestructura.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Asignacion.Carga.Pruebas.Infraestructura;

public class UnificadorArchivosTests : IDisposable
{
    private const string Variable = "ASIGNACION_ZIP_PASSWORD_PRUEBA";
    private const string Contrasena = "clave-de-prueba";
    private const string Encabezado = "CLIENTE_UNICO|NOMBRE_CTE|GESTORES|TIPO_GESTION_|FECHA_ASIGNACION|SALDO";
    private static readonly DateOnly Hoy = new(2026, 11, 18);

    private readonly string _raiz = Directory.CreateTempSubdirectory("asig_unif_").FullName;
    private readonly string _entrada;
    private readonly string _salida;
    private readonly UnificadorArchivos _unificador;

    public UnificadorArchivosTests()
    {
        _entrada = Directory.CreateDirectory(Path.Combine(_raiz, "ASIGNACIÓN_")).FullName;
        _salida = Path.Combine(_raiz, "CARTERAS", "{MES}");
        Environment.SetEnvironmentVariable(Variable, Contrasena);
        _unificador = new UnificadorArchivos(
            Options.Create(new OpcionesUnificacion { VariableContrasena = Variable, CarpetaTemporal = Path.Combine(_raiz, "tmp") }),
            NullLogger<UnificadorArchivos>.Instance);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(Variable, null);
        try { Directory.Delete(_raiz, recursive: true); } catch { /* temporal */ }
    }

    private ConfiguracionFuente Fuente(bool borrar = true) => new(
        2, "LEGAL", _entrada, "*.xlsx", "CARTERA", 1, "ASIG.LEGAL", 5000, TipoProceso.Unificar,
        "|", _salida, "Cartera_ISB_{ddMMyyyy}.xlsx", "Cartera", "TIPO_GESTION_>GESTORES", borrar);

    [Fact]
    public async Task Unifica_zip_AES_anidado_carpeta_y_sueltos_sin_repetir_y_corrige_GESTORES()
    {
        // scl: 2 filas (una con GESTORES corrido a TIPO_GESTION_) | smart: 1 fila
        var scl = Excel(Path.Combine(_raiz, "src", "Descarga_cartera281.xlsx"),
            "1-1-1|ANA|BANCO AZTECA||30/08/2026|19771", "1-1-2|LUIS||MAXIMUS|29/06/2026|49259.13");
        var smart = Excel(Path.Combine(_raiz, "src", "Descarga_cartera0.xlsx"), "1-1-3|EVA|HIBRIDO||10/10/2026|0");

        // Zip cifrado (AES) que trae dentro otro zip cifrado con scl: como "LEGAL ENCRIP 18.zip"
        var interno = ZipAes(Path.Combine(_raiz, "src", "Lote_Segmento_28.zip"), scl);
        ZipAes(Path.Combine(_entrada, "LEGAL ENCRIP 18.zip"), interno);
        // El mismo scl, ya descomprimido en una carpeta: debe tomarse UNA sola vez
        Directory.CreateDirectory(Path.Combine(_entrada, "legal 18", "scl"));
        File.Copy(scl, Path.Combine(_entrada, "legal 18", "scl", "Descarga_cartera281.xlsx"));
        // smart suelto + un temporal de Office que debe ignorarse
        File.Copy(smart, Path.Combine(_entrada, "Descarga_cartera0.xlsx"));
        File.WriteAllText(Path.Combine(_entrada, "~$Descarga_cartera0.xlsx"), "bloqueo");

        var u = await _unificador.UnificarAsync(Fuente(), Hoy, CancellationToken.None);

        Assert.NotNull(u);
        Assert.Equal(2, u.ArchivosUnificados);
        Assert.Equal(1, u.ArchivosRepetidos);
        Assert.Equal(3, u.Filas);
        Assert.Equal(3, u.Entradas.Count); // zip, carpeta y suelto (el ~$ no cuenta)
        Assert.Equal("Cartera_ISB_18112026.xlsx", u.Destino.Nombre);
        Assert.EndsWith(Path.Combine("CARTERAS", "NOVIEMBRE"), u.Destino.Carpeta);

        // El unificado se lee como cualquier Excel: columnas normales, GESTORES corregido
        var lector = new LectorExcelOpenXml();
        var encabezados = await lector.LeerEncabezadosAsync(u.Lectura, "Cartera", 1, CancellationToken.None);
        var filas = await lector.LeerFilasAsync(u.Lectura, "Cartera", 1, CancellationToken.None).ToListAsync();
        Assert.Equal(Encabezado.Split('|'), encabezados);
        var luis = filas.Single(f => (string?)f.Valores["CLIENTE_UNICO"] == "1-1-2");
        Assert.Equal("MAXIMUS", luis.Valores["GESTORES"]);
        Assert.Null(luis.Valores.GetValueOrDefault("TIPO_GESTION_")); // quedó en blanco
        Assert.Equal(49259.13, luis.Valores["SALDO"]);               // número de verdad en Excel
        Assert.Equal(new DateTime(2026, 6, 29), luis.Valores["FECHA_ASIGNACION"]); // fecha de verdad en Excel

        // Publicar crea la carpeta del mes; vaciar deja la entrada limpia (menos el ~$)
        await _unificador.PublicarAsync(u, CancellationToken.None);
        Assert.True(File.Exists(u.Destino.RutaCompleta));
        Assert.Empty(await _unificador.VaciarEntradaAsync(u, CancellationToken.None));
        Assert.Equal(["~$Descarga_cartera0.xlsx"], Directory.EnumerateFileSystemEntries(_entrada).Select(Path.GetFileName));

        await _unificador.DescartarTemporalesAsync(u);
        Assert.False(Directory.Exists(u.CarpetaTemporal));
    }

    [Fact]
    public async Task Fila_identica_en_varios_archivos_o_en_el_mismo_queda_una_sola_y_si_cambia_un_campo_quedan_ambas()
    {
        const string repetida = "1-1-9|ANA|MAXIMUS||19/11/2026|100";
        // La misma fila en 3 archivos distintos y además 2 veces en el primero
        Excel(Path.Combine(_entrada, "a.xlsx"), repetida, "1-1-1|LUIS|HIBRIDO||19/11/2026|5", repetida);
        Excel(Path.Combine(_entrada, "b.xlsx"), repetida);
        // Misma cuenta pero con un campo distinto (SALDO): se queda
        Excel(Path.Combine(_entrada, "c.xlsx"), repetida, "1-1-9|ANA|MAXIMUS||19/11/2026|200");

        var u = await _unificador.UnificarAsync(Fuente(), Hoy, CancellationToken.None);

        Assert.NotNull(u);
        Assert.Equal(3, u.Filas);          // 1-1-9 (100), 1-1-1, 1-1-9 (200)
        Assert.Equal(3, u.FilasRepetidas); // 4 copias de la misma fila → se quedó 1
        var filas = await new LectorExcelOpenXml().LeerFilasAsync(u.Lectura, "Cartera", 1, CancellationToken.None).ToListAsync();
        Assert.Equal([100d, 200d], filas.Where(f => (string?)f.Valores["CLIENTE_UNICO"] == "1-1-9").Select(f => f.Valores["SALDO"]));
    }

    // ---------- Reglas LEGAL (mismo proceso que el Power Query DETALLE_CARTERA_V2) ----------

    private const string EncabezadoPq = "CLIENTE_UNICO|NOMBRE_DESPACHO|CP_CTE|SALDO|FECHA_ASIGNACION|TELEFONO1|CAMPANIA|PREVENTA|GESTORES|TIPO_QUEJA";

    private static OpcionesReglas ReglasLegal() => new()
    {
        Grupos =
        [
            new() { Nombre = "SCL", Patrones = ["scl", "LT*_Segmento*"] },
            new() { Nombre = "SMART", Patrones = ["smart", "Cartera_Seg*"], Renombrar = { ["PREVENTA"] = "TIPO_CARTERA", ["GESTORES"] = "TIPO_GESTION" } },
        ],
        ColumnasExcluidas = ["TIPO_QUEJA"],
        Reemplazos = [new() { Columnas = ["TELEFONO1"], Valor = "N/A", Por = "0" }],
        Tipos =
        [
            new() { Tipo = TipoColumna.Entero, Columnas = ["CP_CTE", "TELEFONO1"] },
            new() { Tipo = TipoColumna.Decimal, Columnas = ["SALDO"] },
            new() { Tipo = TipoColumna.Fecha, Columnas = ["FECHA_ASIGNACION"] },
        ],
        FilasExcluidas = [new() { Columna = "CAMPANIA", EmpiezaCon = ["QUEJA", "CLON"] }],
        ColumnasNuevas =
        [
            new() { Nombre = "NUEVA/REASIGNADA", DespuesDe = "CLIENTE_UNICO", Resaltar = true },
            new() { Nombre = "TIPO_ASIG", DespuesDe = "CLIENTE_UNICO", Resaltar = true },
            new() { Nombre = "SUB TIPO", DespuesDe = "CLIENTE_UNICO", Resaltar = true },
            new() { Nombre = "ESTATUS_TEL", DespuesDe = "CLIENTE_UNICO", Resaltar = true },
            new() { Nombre = "NOMBRE_DESPACHO_", DespuesDe = "NOMBRE_DESPACHO", Valor = "58721-INTEGRATED SOLUTIONS BUREAU" },
            new() { Nombre = "TIPO_GESTION_", DespuesDe = "GESTORES", CopiaDe = "GESTORES" },
        ],
        ColumnasDuplicado = ["CLIENTE_UNICO"],
    };

    private UnificadorArchivos UnificadorLegal() => new(
        Options.Create(new OpcionesUnificacion
        {
            VariableContrasena = Variable,
            CarpetaTemporal = Path.Combine(_raiz, "tmp"),
            Reglas = { ["legal"] = ReglasLegal() }
        }),
        NullLogger<UnificadorArchivos>.Instance);

    [Fact]
    public async Task Legal_replica_el_Power_Query_SCL_y_SMART_filtros_tipos_columnas_nuevas_renombre_y_duplicados()
    {
        // SCL: carpeta scl (como la deja el usuario)
        ExcelConEncabezado(Path.Combine(_entrada, "legal 19", "scl", "Descarga_cartera281.xlsx"), EncabezadoPq,
            "1-1-1|DESP 1|09700|10.50|19/11/2026|N/A|NORMAL|SIN PROYECTO|BANCO AZTECA|Q1",
            "1-1-2|DESP 1|S/N|x|N/A|5512345678|QUEJA 1|P|BANCO AZTECA|",            // CAMPANIA QUEJA: se descarta
            EncabezadoPq,                                                           // encabezado repetido: se descarta
            "1-1-9|DESP 1|1|1|19/11/2026|1|NORMAL|P|HIBRIDO|");                    // también viene en SMART: gana SCL
        // SMART: zip cifrado con el nombre que trae del banco (aunque por nombre quede antes que la carpeta, SCL va primero)
        var smart = ExcelConEncabezado(Path.Combine(_raiz, "src", "Descarga_cartera1.xlsx"), EncabezadoPq,
            "1-1-3|DESP 2|02000|7|01/01/0001|N/A|NORMAL|SALDOS ALTOS|MAXIMUS|",
            "1-1-9|OTRO|2|2|19/11/2026|2|NORMAL|P|HIBRIDO|");
        ZipAes(Path.Combine(_entrada, "Cartera_Seg_19-11-2026 (1).zip"), smart);

        var u = await UnificadorLegal().UnificarAsync(Fuente(), Hoy, CancellationToken.None);

        Assert.NotNull(u);
        Assert.Equal(3, u.Filas);
        Assert.Equal(1, u.FilasExcluidas);
        Assert.Equal(1, u.FilasRepetidas);
        Assert.Equal(new Dictionary<string, int> { ["SCL"] = 2, ["SMART"] = 1 }, u.FilasPorGrupo);

        var lector = new LectorExcelOpenXml();
        Assert.Equal(
            ["CLIENTE_UNICO", "NUEVA/REASIGNADA", "TIPO_ASIG", "SUB TIPO", "ESTATUS_TEL", "NOMBRE_DESPACHO", "NOMBRE_DESPACHO_", "CP_CTE", "SALDO",
             "FECHA_ASIGNACION", "TELEFONO1", "CAMPANIA", "PREVENTA", "GESTORES", "TIPO_GESTION_", "TIPO_CARTERA", "TIPO_GESTION"],
            await lector.LeerEncabezadosAsync(u.Lectura, "Cartera", 1, CancellationToken.None));
        var filas = (await lector.LeerFilasAsync(u.Lectura, "Cartera", 1, CancellationToken.None).ToListAsync())
            .ToDictionary(f => (string)f.Valores["CLIENTE_UNICO"]!, f => f.Valores);

        var scl = filas["1-1-1"];
        Assert.Null(scl.GetValueOrDefault("TIPO_ASIG"));                         // en blanco, la llena WFM
        Assert.Equal("58721-INTEGRATED SOLUTIONS BUREAU", scl["NOMBRE_DESPACHO_"]);
        Assert.Equal(9700d, scl["CP_CTE"]);                                      // entero (como Power Query)
        Assert.Equal(10.5d, scl["SALDO"]);
        Assert.Equal(0d, scl["TELEFONO1"]);                                      // N/A → 0
        Assert.Equal("BANCO AZTECA", scl["GESTORES"]);
        Assert.Equal("BANCO AZTECA", scl["TIPO_GESTION_"]);
        Assert.Null(scl.GetValueOrDefault("TIPO_CARTERA"));                      // en SCL no se renombra

        var sm = filas["1-1-3"];
        Assert.Null(sm.GetValueOrDefault("PREVENTA"));                           // en SMART, PREVENTA → TIPO_CARTERA
        Assert.Null(sm.GetValueOrDefault("GESTORES"));                           // y GESTORES → TIPO_GESTION
        Assert.Equal("SALDOS ALTOS", sm["TIPO_CARTERA"]);
        Assert.Equal("MAXIMUS", sm["TIPO_GESTION"]);
        Assert.Equal("MAXIMUS", sm["TIPO_GESTION_"]);                             // copia tomada antes de renombrar
        Assert.Null(sm.GetValueOrDefault("FECHA_ASIGNACION"));                   // 01/01/0001 → vacío
        Assert.Equal(2000d, sm["CP_CTE"]);

        Assert.Equal("DESP 1", filas["1-1-9"]["NOMBRE_DESPACHO"]);               // duplicada: se quedó la de SCL

        // Los 4 encabezados que llena WFM van en verde
        using var doc = SpreadsheetDocument.Open(u.Lectura.RutaCompleta, false);
        var encabezado = doc.WorkbookPart!.WorksheetParts.Single().Worksheet!.Descendants<Row>().First().Elements<Cell>().ToList();
        Assert.Equal([false, true, true, true, true, false], encabezado.Take(6).Select(c => c.StyleIndex?.Value == 2));
    }

    [Fact]
    public async Task Legal_archivo_que_no_es_SCL_ni_SMART_detiene_la_unificacion()
    {
        ExcelConEncabezado(Path.Combine(_entrada, "otra carpeta", "Descarga_cartera9.xlsx"), EncabezadoPq, "1|D|1|1|19/11/2026|1|NORMAL|P|G|");

        var ex = await Assert.ThrowsAsync<DominioException>(() => UnificadorLegal().UnificarAsync(Fuente(), Hoy, CancellationToken.None));

        Assert.Contains("No se sabe a qué grupo", ex.Message);
        Assert.True(File.Exists(Path.Combine(_entrada, "otra carpeta", "Descarga_cartera9.xlsx"))); // la entrada no se toca
    }

    [Fact]
    public async Task Legal_si_el_banco_quita_una_columna_que_usan_las_reglas_se_detiene_con_mensaje_claro()
    {
        ExcelConEncabezado(Path.Combine(_entrada, "scl", "a.xlsx"), "CLIENTE_UNICO|NOMBRE_DESPACHO|CP_CTE", "1|D|1");

        var ex = await Assert.ThrowsAsync<DominioException>(() => UnificadorLegal().UnificarAsync(Fuente(), Hoy, CancellationToken.None));

        Assert.Contains("no trae la columna", ex.Message);
    }

    [Fact]
    public async Task Entrada_vacia_devuelve_null()
    {
        Assert.Null(await _unificador.UnificarAsync(Fuente(), Hoy, CancellationToken.None));
    }

    [Fact]
    public async Task Zip_cifrado_sin_contrasena_da_error_claro_y_no_toca_la_entrada()
    {
        Environment.SetEnvironmentVariable(Variable, null);
        var x = Excel(Path.Combine(_raiz, "src", "a.xlsx"), "1|A|B||01/01/2026|1");
        ZipAes(Path.Combine(_entrada, "LEGAL ENCRIP.zip"), x);

        var ex = await Assert.ThrowsAsync<DominioException>(() => _unificador.UnificarAsync(Fuente(), Hoy, CancellationToken.None));

        Assert.Contains("contraseña", ex.Message);
        Assert.True(File.Exists(Path.Combine(_entrada, "LEGAL ENCRIP.zip")));
    }

    [Fact]
    public async Task Archivos_con_distintos_encabezados_no_se_unifican()
    {
        Excel(Path.Combine(_entrada, "a.xlsx"), "1|A|B||01/01/2026|1");
        ExcelConEncabezado(Path.Combine(_entrada, "b.xlsx"), "CLIENTE_UNICO|OTRA", "1|X");

        var ex = await Assert.ThrowsAsync<DominioException>(() => _unificador.UnificarAsync(Fuente(), Hoy, CancellationToken.None));

        Assert.Contains("mismos encabezados", ex.Message);
    }

    [Fact]
    public async Task Fila_con_campos_de_mas_o_de_menos_detiene_la_unificacion()
    {
        Excel(Path.Combine(_entrada, "a.xlsx"), "1|A|B||01/01/2026|1", "2|SOLO-DOS");

        var ex = await Assert.ThrowsAsync<DominioException>(() => _unificador.UnificarAsync(Fuente(), Hoy, CancellationToken.None));

        Assert.Contains("fila 3", ex.Message);
    }

    [Theory]
    [InlineData("19771", true)]
    [InlineData("-106.40616", true)]
    [InlineData("0", true)]
    [InlineData("00127041", false)]          // cero a la izquierda: texto
    [InlineData("127972010127128992", false)] // 18 dígitos: texto
    [InlineData("N/A", false)]
    public void Escritor_solo_convierte_a_numero_lo_que_Excel_no_echaria_a_perder(string valor, bool esNumero)
    {
        var celda = EscritorExcelOpenXml.Celda(valor);

        Assert.Equal(esNumero, celda.DataType is null && celda.StyleIndex is null);
    }

    // ---------- ayudantes ----------

    private static string Excel(string ruta, params string[] filas) => ExcelConEncabezado(ruta, Encabezado, filas);

    private static string ExcelConEncabezado(string ruta, string encabezado, params string[] filas)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        using var doc = SpreadsheetDocument.Create(ruta, SpreadsheetDocumentType.Workbook);
        var libro = doc.AddWorkbookPart();
        libro.Workbook = new Workbook(new Sheets());
        var hoja = libro.AddNewPart<WorksheetPart>();
        var datos = new SheetData();
        uint n = 1;
        foreach (var texto in new[] { encabezado }.Concat(filas))
            datos.Append(new Row(new Cell { DataType = CellValues.InlineString, InlineString = new InlineString(new Text(texto)) }) { RowIndex = n++ });
        hoja.Worksheet = new Worksheet(datos);
        libro.Workbook.Sheets!.Append(new Sheet { Name = "CARTERA", SheetId = 1, Id = libro.GetIdOfPart(hoja) });
        return ruta;
    }

    private static string ZipAes(string ruta, string archivo)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        using (var zip = new ZipOutputStream(File.Create(ruta)) { Password = Contrasena })
        {
            zip.PutNextEntry(new ZipEntry(Path.GetFileName(archivo)) { AESKeySize = 256 });
            using (var f = File.OpenRead(archivo)) f.CopyTo(zip);
            zip.CloseEntry();
        }
        return ruta;
    }
}
