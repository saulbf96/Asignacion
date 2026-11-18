using System.Globalization;
using System.Text.RegularExpressions;
using Asignacion.Carga.Dominio.Excepciones;

namespace Asignacion.Carga.Dominio.Entidades;

/// <summary>
/// Configuración de una fuente de archivos (registro de ASIG.CONFIG_FUENTE).
/// </summary>
public sealed partial class ConfiguracionFuente
{
    public int IdFuente { get; }
    public string Nombre { get; }
    public string RutaOrigen { get; }

    /// <summary>Patrón del nombre de archivo, p. ej. <c>BAZv3.2_TEMPRANA_{yyyyMMdd}_*.xlsx</c>.</summary>
    public string PatronArchivo { get; }

    public string Hoja { get; }

    /// <summary>Fila (base 1) donde están los encabezados.</summary>
    public int FilaEncabezado { get; }

    public string TablaDestino { get; }

    /// <summary>Filas que se envían por cada llamada de inserción.</summary>
    public int TamanoLote { get; }

    public ConfiguracionFuente(
        int idFuente, string nombre, string rutaOrigen, string patronArchivo,
        string hoja, int filaEncabezado, string tablaDestino, int tamanoLote)
    {
        if (idFuente <= 0) throw new ConfiguracionInvalidaException("IdFuente debe ser mayor a 0.");
        if (filaEncabezado < 1) throw new ConfiguracionInvalidaException($"Fuente {idFuente}: FilaEncabezado debe ser 1 o mayor.");
        if (tamanoLote < 1) throw new ConfiguracionInvalidaException($"Fuente {idFuente}: TamanoLote debe ser 1 o mayor.");

        IdFuente = idFuente;
        Nombre = Requerido(nombre, nameof(Nombre));
        RutaOrigen = Requerido(rutaOrigen, nameof(RutaOrigen));
        PatronArchivo = Requerido(patronArchivo, nameof(PatronArchivo));
        Hoja = Requerido(hoja, nameof(Hoja));
        TablaDestino = Requerido(tablaDestino, nameof(TablaDestino));
        FilaEncabezado = filaEncabezado;
        TamanoLote = tamanoLote;
    }

    /// <summary>
    /// Reemplaza los marcadores de fecha del patrón (<c>{yyyyMMdd}</c>, <c>{yyyy}</c>, etc.)
    /// por la fecha indicada. El resultado puede conservar comodines (<c>*</c>, <c>?</c>).
    /// </summary>
    public string ResolverPatron(DateOnly fecha) =>
        MarcadorFecha().Replace(PatronArchivo, m => fecha.ToString(m.Groups[1].Value, CultureInfo.InvariantCulture));

    private string Requerido(string valor, string campo) =>
        string.IsNullOrWhiteSpace(valor)
            ? throw new ConfiguracionInvalidaException($"Fuente {IdFuente}: {campo} es obligatorio.")
            : valor.Trim();

    [GeneratedRegex(@"\{([^{}]+)\}")]
    private static partial Regex MarcadorFecha();
}
