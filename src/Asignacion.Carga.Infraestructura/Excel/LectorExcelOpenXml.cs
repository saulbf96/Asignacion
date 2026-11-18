using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Asignacion.Carga.Aplicacion.Interfaces;
using Asignacion.Carga.Dominio.Entidades;
using Asignacion.Carga.Dominio.Excepciones;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Asignacion.Carga.Infraestructura.Excel;

/// <summary>
/// Lee .xlsx fila por fila (modo flujo / SAX) sin cargar el libro completo en memoria.
/// Tolera archivos generados por sistemas: celdas sin referencia (r=""), texto en línea
/// (inlineStr) y filas sin número. El archivo se abre en solo lectura y compartido,
/// así que puede leerse aunque alguien lo tenga abierto en Excel.
/// </summary>
internal sealed partial class LectorExcelOpenXml : ILectorArchivo
{
    public Task<IReadOnlyList<string?>> LeerEncabezadosAsync(ArchivoOrigen archivo, string hoja, int filaEncabezado, CancellationToken ct) =>
        Task.Run<IReadOnlyList<string?>>(() =>
        {
            using var libro = LibroAbierto.Abrir(archivo, hoja);
            foreach (var (numero, celdas) in libro.Filas(ct))
            {
                if (numero == filaEncabezado) return Encabezados(celdas);
                if (numero > filaEncabezado) break;
            }
            throw new DominioException($"La hoja '{hoja}' de '{archivo.Nombre}' no tiene la fila de encabezados {filaEncabezado}.");
        }, ct);

    public async IAsyncEnumerable<FilaArchivo> LeerFilasAsync(
        ArchivoOrigen archivo, string hoja, int filaEncabezado, [EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Yield(); // no bloquear a quien llama mientras se abre el archivo

        using var libro = LibroAbierto.Abrir(archivo, hoja);
        IReadOnlyList<string?>? encabezados = null;

        foreach (var (numero, celdas) in libro.Filas(ct))
        {
            if (numero < filaEncabezado) continue;
            if (numero == filaEncabezado) { encabezados = Encabezados(celdas); continue; }
            if (encabezados is null)
                throw new DominioException($"La hoja '{hoja}' de '{archivo.Nombre}' no tiene la fila de encabezados {filaEncabezado}.");

            var valores = new Dictionary<string, object?>(StringComparer.Ordinal);
            var conDatos = false;
            foreach (var (columna, valor) in celdas)
            {
                if (columna >= encabezados.Count || encabezados[columna] is not { } nombre) continue;
                if (valores.ContainsKey(nombre)) continue; // encabezado repetido: vale el primero
                valores[nombre] = valor;
                conDatos |= valor is not null;
            }
            if (conDatos) yield return new FilaArchivo(numero, valores);
        }
    }

    /// <summary>Encabezados por posición de columna, sin espacios sobrantes.</summary>
    private static IReadOnlyList<string?> Encabezados(IReadOnlyList<(int Columna, object? Valor)> celdas)
    {
        var total = celdas.Count == 0 ? 0 : celdas.Max(c => c.Columna) + 1;
        var lista = new string?[total];
        foreach (var (columna, valor) in celdas)
        {
            var texto = Convert.ToString(valor, CultureInfo.InvariantCulture)?.Trim();
            lista[columna] = string.IsNullOrEmpty(texto) ? null : texto;
        }
        return lista;
    }

    /// <summary>
    /// Libro abierto con lo necesario para interpretar las celdas de una hoja.
    /// También lo usa el unificador para leer los archivos de entrada.
    /// </summary>
    internal sealed class LibroAbierto : IDisposable
    {
        private readonly FileStream _flujo;
        private readonly SpreadsheetDocument _documento;
        private readonly WorksheetPart _hoja;
        private readonly string[] _textosCompartidos;
        private readonly bool[] _estiloEsFecha;

        private LibroAbierto(FileStream flujo, SpreadsheetDocument documento, WorksheetPart hoja, string[] textos, bool[] fechas)
        {
            _flujo = flujo; _documento = documento; _hoja = hoja; _textosCompartidos = textos; _estiloEsFecha = fechas;
        }

        public static LibroAbierto Abrir(ArchivoOrigen archivo, string nombreHoja)
        {
            var flujo = new FileStream(archivo.RutaCompleta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            try
            {
                var documento = SpreadsheetDocument.Open(flujo, false);
                var libro = documento.WorkbookPart ?? throw new DominioException($"'{archivo.Nombre}' no es un libro de Excel válido.");

                var hoja = libro.Workbook?.Sheets?.Elements<Sheet>()
                    .FirstOrDefault(s => string.Equals(s.Name?.Value?.Trim(), nombreHoja.Trim(), StringComparison.OrdinalIgnoreCase))
                    ?? throw new DominioException($"El archivo '{archivo.Nombre}' no tiene la hoja '{nombreHoja}'.");

                return new LibroAbierto(flujo, documento, (WorksheetPart)libro.GetPartById(hoja.Id!.Value!),
                    LeerTextosCompartidos(libro), LeerEstilosDeFecha(libro));
            }
            catch
            {
                flujo.Dispose();
                throw;
            }
        }

        /// <summary>Filas con su número (base 1) y sus celdas (columna base 0, valor).</summary>
        public IEnumerable<(int Numero, IReadOnlyList<(int Columna, object? Valor)> Celdas)> Filas(CancellationToken ct)
        {
            using var lector = OpenXmlReader.Create(_hoja);
            var numero = 0;
            while (lector.Read())
            {
                if (lector.ElementType != typeof(Row) || !lector.IsStartElement) continue;
                ct.ThrowIfCancellationRequested();

                var fila = (Row)lector.LoadCurrentElement()!;
                numero = fila.RowIndex?.Value is { } r ? (int)r : numero + 1;

                var celdas = new List<(int, object?)>();
                var posicion = 0;
                foreach (var celda in fila.Elements<Cell>())
                {
                    var columna = IndiceColumna(celda.CellReference?.Value) ?? posicion;
                    posicion = columna + 1;
                    celdas.Add((columna, Valor(celda)));
                }
                yield return (numero, celdas);
            }
        }

        private object? Valor(Cell celda)
        {
            var tipo = celda.DataType?.Value;
            if (tipo == CellValues.InlineString)
                return Texto(celda.InlineString?.InnerText);

            var crudo = celda.CellValue?.Text;
            if (string.IsNullOrEmpty(crudo)) return null;

            if (tipo == CellValues.SharedString)
                return int.TryParse(crudo, out var i) && i >= 0 && i < _textosCompartidos.Length ? Texto(_textosCompartidos[i]) : null;
            if (tipo == CellValues.String || tipo == CellValues.Error)
                return Texto(crudo);
            if (tipo == CellValues.Boolean)
                return crudo == "1";
            if (tipo == CellValues.Date)
                return DateTime.TryParse(crudo, CultureInfo.InvariantCulture, DateTimeStyles.None, out var f) ? f : Texto(crudo);

            if (!double.TryParse(crudo, NumberStyles.Float, CultureInfo.InvariantCulture, out var numero))
                return Texto(crudo);

            var estilo = (int)(celda.StyleIndex?.Value ?? 0);
            return estilo < _estiloEsFecha.Length && _estiloEsFecha[estilo] && numero is > -657435 and < 2958466
                ? DateTime.FromOADate(numero)
                : numero;
        }

        private static string? Texto(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

        private static string[] LeerTextosCompartidos(WorkbookPart libro)
        {
            var parte = libro.SharedStringTablePart;
            if (parte is null) return [];
            var textos = new List<string>();
            using var lector = OpenXmlReader.Create(parte);
            while (lector.Read())
            {
                if (lector.ElementType == typeof(SharedStringItem) && lector.IsStartElement)
                    textos.Add(((SharedStringItem)lector.LoadCurrentElement()!).InnerText);
            }
            return [.. textos];
        }

        /// <summary>Por cada estilo de celda, si su formato numérico es de fecha u hora.</summary>
        private static bool[] LeerEstilosDeFecha(WorkbookPart libro)
        {
            var hojaEstilos = libro.WorkbookStylesPart?.Stylesheet;
            var formatos = hojaEstilos?.CellFormats?.Elements<CellFormat>().ToList();
            if (formatos is null) return [];

            var personalizados = hojaEstilos!.NumberingFormats?.Elements<NumberingFormat>()
                .Where(n => n.NumberFormatId?.Value is not null)
                .ToDictionary(n => n.NumberFormatId!.Value, n => n.FormatCode?.Value ?? "")
                ?? [];

            return formatos.Select(f =>
            {
                var id = f.NumberFormatId?.Value ?? 0;
                return EsFormatoFechaPredefinido(id)
                       || (personalizados.TryGetValue(id, out var codigo) && EsCodigoFecha(codigo));
            }).ToArray();
        }

        private static bool EsFormatoFechaPredefinido(uint id) => id is (>= 14 and <= 22) or (>= 27 and <= 36) or (>= 45 and <= 47) or (>= 50 and <= 58);

        private static bool EsCodigoFecha(string codigo)
        {
            // Quita texto entre comillas, [colores/locales] y escapes antes de buscar d, y, h o s
            var limpio = Limpieza().Replace(codigo, "");
            return limpio.IndexOfAny(['d', 'D', 'y', 'Y', 'h', 'H', 's', 'S']) >= 0
                   && !limpio.Contains("General", StringComparison.OrdinalIgnoreCase);
        }

        public void Dispose()
        {
            _documento.Dispose();
            _flujo.Dispose();
        }
    }

    /// <summary>"BC12" → 54 (base 0). Null si la referencia viene vacía o es inválida.</summary>
    internal static int? IndiceColumna(string? referencia)
    {
        if (string.IsNullOrEmpty(referencia)) return null;
        var indice = 0; var letras = 0;
        foreach (var c in referencia)
        {
            if (c is >= 'A' and <= 'Z') indice = indice * 26 + (c - 'A' + 1);
            else if (c is >= 'a' and <= 'z') indice = indice * 26 + (c - 'a' + 1);
            else break;
            letras++;
        }
        return letras == 0 ? null : indice - 1;
    }

    [GeneratedRegex("\"[^\"]*\"|\\[[^\\]]*\\]|\\\\.")]
    private static partial Regex Limpieza();
}
