using System.Globalization;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Asignacion.Carga.Infraestructura.Excel;

/// <summary>
/// Escribe un .xlsx de una hoja en modo flujo (SAX), sin armar el libro en memoria.
/// Para que el archivo sea cómodo en Excel: los números "limpios" se escriben como número,
/// las fechas dd/MM/yyyy como fecha y todo lo demás como texto. Se conservan como texto los
/// valores con ceros a la izquierda o con más de 15 dígitos (Excel los perdería).
/// </summary>
internal static partial class EscritorExcelOpenXml
{
    private const uint EstiloFecha = 1;

    /// <summary>Escribe el archivo y devuelve las filas de datos escritas.</summary>
    public static int Escribir(string ruta, string nombreHoja, IReadOnlyList<string> encabezados, IEnumerable<string?[]> filas, CancellationToken ct)
    {
        var escritas = 0;
        using var documento = SpreadsheetDocument.Create(ruta, SpreadsheetDocumentType.Workbook);
        var libro = documento.AddWorkbookPart();
        libro.Workbook = new Workbook();

        var estilos = libro.AddNewPart<WorkbookStylesPart>();
        estilos.Stylesheet = new Stylesheet(
            new Fonts(new Font()),
            new Fills(new Fill(new PatternFill { PatternType = PatternValues.None }), new Fill(new PatternFill { PatternType = PatternValues.Gray125 })),
            new Borders(new Border()),
            new CellStyleFormats(new CellFormat { NumberFormatId = 0, FontId = 0, FillId = 0, BorderId = 0 }),
            new CellFormats(
                new CellFormat { NumberFormatId = 0, FontId = 0, FillId = 0, BorderId = 0, FormatId = 0 },
                new CellFormat { NumberFormatId = 14, FontId = 0, FillId = 0, BorderId = 0, FormatId = 0, ApplyNumberFormat = true }), // 14 = fecha corta
            new CellStyles(new CellStyle { Name = "Normal", FormatId = 0, BuiltinId = 0 }));

        var parteHoja = libro.AddNewPart<WorksheetPart>();
        using (var w = OpenXmlWriter.Create(parteHoja))
        {
            w.WriteStartElement(new Worksheet());
            w.WriteStartElement(new SheetData());

            w.WriteStartElement(new Row());
            foreach (var e in encabezados) w.WriteElement(Texto(e));
            w.WriteEndElement();

            foreach (var fila in filas)
            {
                ct.ThrowIfCancellationRequested();
                w.WriteStartElement(new Row());
                for (var i = 0; i < encabezados.Count; i++)
                    w.WriteElement(Celda(i < fila.Length ? fila[i] : null));
                w.WriteEndElement();
                escritas++;
            }

            w.WriteEndElement(); // SheetData
            w.WriteEndElement(); // Worksheet
        }

        libro.Workbook.AppendChild(new Sheets(new Sheet { Id = libro.GetIdOfPart(parteHoja), SheetId = 1, Name = nombreHoja }));
        libro.Workbook.Save();
        return escritas;
    }

    internal static Cell Celda(string? valor)
    {
        if (string.IsNullOrEmpty(valor)) return new Cell();

        if (NumeroLimpio().IsMatch(valor))
            return new Cell { CellValue = new CellValue(valor) };

        if (FechaDmy().IsMatch(valor) &&
            DateTime.TryParseExact(valor, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
            return new Cell { CellValue = new CellValue(fecha.ToOADate().ToString(CultureInfo.InvariantCulture)), StyleIndex = EstiloFecha };

        return Texto(valor);
    }

    private static Cell Texto(string valor) => new()
    {
        DataType = CellValues.InlineString,
        InlineString = new InlineString(new Text(valor) { Space = SpaceProcessingModeValues.Preserve })
    };

    /// <summary>Entero o decimal sin ceros a la izquierda y con máximo 15 dígitos enteros.</summary>
    [GeneratedRegex(@"^-?(0|[1-9]\d{0,14})(\.\d{1,10})?$")]
    private static partial Regex NumeroLimpio();

    [GeneratedRegex(@"^\d{2}/\d{2}/\d{4}$")]
    private static partial Regex FechaDmy();
}
