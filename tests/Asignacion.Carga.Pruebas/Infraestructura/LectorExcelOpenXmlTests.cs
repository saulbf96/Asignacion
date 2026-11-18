using System.Diagnostics;
using Asignacion.Carga.Dominio.Entidades;
using Asignacion.Carga.Dominio.Excepciones;
using Asignacion.Carga.Infraestructura.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Xunit.Abstractions;

namespace Asignacion.Carga.Pruebas.Infraestructura;

public class LectorExcelOpenXmlTests : IDisposable
{
    private readonly ITestOutputHelper _salida;
    private readonly string _carpeta = Directory.CreateTempSubdirectory("asig_excel_").FullName;
    private readonly LectorExcelOpenXml _lector = new();

    public LectorExcelOpenXmlTests(ITestOutputHelper salida) => _salida = salida;

    public void Dispose() => Directory.Delete(_carpeta, recursive: true);

    [Theory]
    [InlineData("A1", 0)]
    [InlineData("Z9", 25)]
    [InlineData("AA10", 26)]
    [InlineData("BC2", 54)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void IndiceColumna(string? referencia, int? esperado)
    {
        Assert.Equal(esperado, LectorExcelOpenXml.IndiceColumna(referencia));
    }

    /// <summary>
    /// Imita lo raro del archivo real: fila 1 vacía, encabezados en la fila 2,
    /// celdas SIN referencia (r=""), texto en línea, texto compartido, fechas por estilo y una hoja extra.
    /// </summary>
    [Fact]
    public async Task Lee_archivo_con_celdas_sin_referencia_texto_en_linea_y_fechas()
    {
        var archivo = CrearLibroDePrueba();

        var encabezados = await _lector.LeerEncabezadosAsync(archivo, "ASIGNACION", 2, CancellationToken.None);
        var filas = await _lector.LeerFilasAsync(archivo, "ASIGNACION", 2, CancellationToken.None).ToListAsync();

        Assert.Equal(["IDUnico", "saldo", "fecha Vencimiento", "nombre"], encabezados);
        Assert.Equal(2, filas.Count); // la fila totalmente vacía se omite
        var f = filas[0];
        Assert.Equal("1-1-5953-140130", f.Valores["IDUnico"]);
        Assert.Equal(19764.5, f.Valores["saldo"]);
        Assert.Equal(new DateTime(2026, 11, 18), f.Valores["fecha Vencimiento"]);
        Assert.Equal("ALEXIE FUJIMOTO", f.Valores["nombre"]);
        Assert.Equal("1-11-661-1248", filas[1].Valores["IDUnico"]);
        Assert.False(filas[1].Valores.ContainsKey("saldo")); // celda inexistente = sin valor
    }

    [Fact]
    public async Task Hoja_inexistente_lanza_excepcion_clara()
    {
        var archivo = CrearLibroDePrueba();

        var ex = await Assert.ThrowsAsync<DominioException>(
            () => _lector.LeerEncabezadosAsync(archivo, "NO_EXISTE", 2, CancellationToken.None));
        Assert.Contains("NO_EXISTE", ex.Message);
    }

    /// <summary>
    /// Lee el archivo REAL de DOCUMENTOS, si está en la máquina (si no, la prueba no hace nada).
    /// Se puede apuntar a otro con la variable de entorno ASIGNACION_ARCHIVO_PRUEBA.
    /// </summary>
    [Fact]
    public async Task Archivo_real_BAZ_TEMPRANA_se_lee_completo()
    {
        var ruta = Environment.GetEnvironmentVariable("ASIGNACION_ARCHIVO_PRUEBA")
                   ?? @"C:\Users\sbasilio\Desktop\Trabajos\ASIGNACION\DOCUMENTOS\BAZv3.2_TEMPRANA_20261118_064936.xlsx";
        if (!File.Exists(ruta))
        {
            _salida.WriteLine($"Se omite: no existe {ruta}");
            return;
        }
        var archivo = new ArchivoOrigen(Path.GetFileName(ruta), Path.GetDirectoryName(ruta)!, new DateOnly(2026, 11, 18), File.GetLastWriteTime(ruta));
        var reloj = Stopwatch.StartNew();

        var encabezados = await _lector.LeerEncabezadosAsync(archivo, "ASIGNACION", 2, CancellationToken.None);
        var total = 0;
        FilaArchivo? primera = null;
        await foreach (var fila in _lector.LeerFilasAsync(archivo, "ASIGNACION", 2, CancellationToken.None))
        {
            primera ??= fila;
            total++;
        }

        _salida.WriteLine($"{total} filas leídas en {reloj.Elapsed.TotalSeconds:N1} s; memoria {GC.GetTotalMemory(false) / 1024 / 1024} MB");
        Assert.Equal(55, encabezados.Count(e => e is not null));
        Assert.Equal(50256, total);
        Assert.Equal("1-1-5953-140130", primera!.Valores["IDUnico"]);
        Assert.Equal(19764.0, primera.Valores["saldo"]);
        Assert.Equal(new DateTime(2026, 11, 24), primera.Valores["fecha Proxima Pago"]);
        Assert.Equal("7717015350", primera.Valores["telefono1"]);
    }

    private ArchivoOrigen CrearLibroDePrueba()
    {
        var ruta = Path.Combine(_carpeta, "prueba.xlsx");
        using (var doc = SpreadsheetDocument.Create(ruta, SpreadsheetDocumentType.Workbook))
        {
            var libro = doc.AddWorkbookPart();
            libro.Workbook = new Workbook(new Sheets());

            var estilos = libro.AddNewPart<WorkbookStylesPart>();
            estilos.Stylesheet = new Stylesheet(
                new Fonts(new Font()), new Fills(new Fill()), new Borders(new Border()),
                new CellFormats(new CellFormat(), new CellFormat { NumberFormatId = 14, ApplyNumberFormat = true }));

            var textos = libro.AddNewPart<SharedStringTablePart>();
            textos.SharedStringTable = new SharedStringTable(new SharedStringItem(new Text("ALEXIE FUJIMOTO")));

            AgregarHoja(libro, "RESUMEN", 1, new SheetData());

            Cell EnLinea(string texto, string? r = null) => new()
            {
                CellReference = r, DataType = CellValues.InlineString, InlineString = new InlineString(new Text(texto))
            };

            var datos = new SheetData(
                new Row { RowIndex = 1 },
                new Row(EnLinea("IDUnico"), EnLinea("saldo"), EnLinea("fecha Vencimiento"), EnLinea("nombre")) { RowIndex = 2 },
                new Row(
                    EnLinea("1-1-5953-140130"),
                    new Cell { CellValue = new CellValue("19764.5") },
                    new Cell { CellValue = new CellValue("46344"), StyleIndex = 1 },
                    new Cell { DataType = CellValues.SharedString, CellValue = new CellValue("0") }) { RowIndex = 3 },
                new Row(new Cell(), new Cell()) { RowIndex = 4 },
                new Row(EnLinea("1-11-661-1248", "A5")) { RowIndex = 5 });
            AgregarHoja(libro, "ASIGNACION", 2, datos);
            AgregarHoja(libro, "RETIROS", 3, new SheetData(new Row(EnLinea("NO LEER")) { RowIndex = 2 }));
        }
        return new ArchivoOrigen("prueba.xlsx", _carpeta, new DateOnly(2026, 11, 18), DateTime.Now);
    }

    private static void AgregarHoja(WorkbookPart libro, string nombre, uint id, SheetData datos)
    {
        var parte = libro.AddNewPart<WorksheetPart>();
        parte.Worksheet = new Worksheet(datos);
        libro.Workbook!.Sheets!.Append(new Sheet { Name = nombre, SheetId = id, Id = libro.GetIdOfPart(parte) });
    }
}
