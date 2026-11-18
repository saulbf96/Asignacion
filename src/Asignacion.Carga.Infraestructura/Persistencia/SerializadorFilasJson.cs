using System.Globalization;
using System.Text;
using System.Text.Json;
using Asignacion.Carga.Dominio.Entidades;

namespace Asignacion.Carga.Infraestructura.Persistencia;

/// <summary>
/// Convierte las filas del archivo al JSON que espera @Datos de ASIG.SP_CARGA:
/// [{"encabezado":"valor", ...}, ...] con todos los valores como texto.
/// Números con punto decimal y sin notación científica; fechas yyyy-MM-dd.
/// Las celdas vacías no se mandan (el SP las deja en NULL).
/// </summary>
internal static class SerializadorFilasJson
{
    private static readonly JsonWriterOptions Opciones = new() { Indented = false };

    public static string Serializar(IReadOnlyList<FilaArchivo> filas)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer, Opciones))
        {
            json.WriteStartArray();
            foreach (var fila in filas)
            {
                json.WriteStartObject();
                foreach (var (encabezado, valor) in fila.Valores)
                {
                    var texto = ATexto(valor);
                    if (texto is not null)
                        json.WriteString(encabezado, texto);
                }
                json.WriteEndObject();
            }
            json.WriteEndArray();
        }
        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    internal static string? ATexto(object? valor) => valor switch
    {
        null => null,
        string s => string.IsNullOrWhiteSpace(s) ? null : s,
        DateTime f when f.TimeOfDay == TimeSpan.Zero => f.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime f => f.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        DateOnly f => f.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        bool b => b ? "1" : "0",
        double d => Numero(d),
        float f => Numero(f),
        decimal m => m.ToString(CultureInfo.InvariantCulture),
        IFormattable n => n.ToString(null, CultureInfo.InvariantCulture),
        _ => valor.ToString()
    };

    /// <summary>
    /// Excel guarda los números como double. Pasarlos por decimal evita "1E+15" y
    /// errores de representación (899.07 en vez de 899.0700000001).
    /// </summary>
    private static string Numero(double d)
    {
        if (double.IsNaN(d) || double.IsInfinity(d)) return string.Empty;
        return d is > (double)decimal.MaxValue or < (double)decimal.MinValue
            ? d.ToString("R", CultureInfo.InvariantCulture)
            : ((decimal)d).ToString(CultureInfo.InvariantCulture);
    }
}
