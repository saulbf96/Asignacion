using System.Globalization;
using System.Text.RegularExpressions;
using Asignacion.Carga.Dominio.Enums;
using Asignacion.Carga.Dominio.Excepciones;

namespace Asignacion.Carga.Dominio.Entidades;

/// <summary>
/// Configuración de una fuente de archivos (registro de ASIG.CONFIG_FUENTE).
/// </summary>
public sealed partial class ConfiguracionFuente
{
    private static readonly CultureInfo Espanol = CultureInfo.GetCultureInfo("es-MX");

    public int IdFuente { get; }
    public string Nombre { get; }
    public string RutaOrigen { get; }

    /// <summary>Patrón del nombre de archivo, p. ej. <c>BAZv3.2_TEMPRANA_{yyyyMMdd}_*.xlsx</c>.</summary>
    public string PatronArchivo { get; }

    /// <summary>Hoja a leer de cada archivo de entrada.</summary>
    public string Hoja { get; }

    /// <summary>Fila (base 1) donde están los encabezados en cada archivo de entrada.</summary>
    public int FilaEncabezado { get; }

    public string TablaDestino { get; }

    /// <summary>Filas que se envían por cada llamada de inserción.</summary>
    public int TamanoLote { get; }

    public TipoProceso TipoProceso { get; }

    /// <summary>Solo <see cref="TipoProceso.Unificar"/>: separador de campos dentro de la celda (p. ej. "|").</summary>
    public string? Separador { get; }

    /// <summary>Solo Unificar: carpeta donde se guarda el unificado, p. ej. <c>...\CARTERAS\{MES}</c>.</summary>
    public string? RutaSalida { get; }

    /// <summary>Solo Unificar: nombre del unificado, p. ej. <c>Cartera_ISB_{ddMMyyyy}.xlsx</c>.</summary>
    public string? NombreSalida { get; }

    /// <summary>Solo Unificar: nombre de la hoja del unificado.</summary>
    public string HojaSalida { get; }

    public IReadOnlyList<CorreccionColumna> CorreccionesColumnas { get; }

    /// <summary>Solo Unificar: vaciar la carpeta de entrada cuando la carga termina bien.</summary>
    public bool BorrarEntrada { get; }

    public ConfiguracionFuente(
        int idFuente, string nombre, string rutaOrigen, string patronArchivo,
        string hoja, int filaEncabezado, string tablaDestino, int tamanoLote,
        TipoProceso tipoProceso = TipoProceso.ArchivoDiario,
        string? separador = null, string? rutaSalida = null, string? nombreSalida = null,
        string? hojaSalida = null, string? correccionColumnas = null, bool borrarEntrada = false)
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
        TipoProceso = tipoProceso;
        CorreccionesColumnas = CorreccionColumna.Leer(correccionColumnas);
        BorrarEntrada = borrarEntrada;
        HojaSalida = string.IsNullOrWhiteSpace(hojaSalida) ? "Hoja1" : hojaSalida.Trim();

        if (tipoProceso == TipoProceso.Unificar)
        {
            Separador = string.IsNullOrEmpty(separador)
                ? throw new ConfiguracionInvalidaException($"Fuente {idFuente}: Separador es obligatorio para UNIFICAR.")
                : separador;
            RutaSalida = Requerido(rutaSalida ?? "", nameof(RutaSalida));
            NombreSalida = Requerido(nombreSalida ?? "", nameof(NombreSalida));
        }
    }

    /// <summary>
    /// Reemplaza los marcadores de fecha del patrón (<c>{yyyyMMdd}</c>, <c>{MES}</c>, etc.).
    /// El resultado puede conservar comodines (<c>*</c>, <c>?</c>).
    /// </summary>
    public string ResolverPatron(DateOnly fecha) => Resolver(PatronArchivo, fecha);

    /// <summary>Carpeta del unificado para la fecha, p. ej. <c>...\CARTERAS\NOVIEMBRE</c>.</summary>
    public string ResolverRutaSalida(DateOnly fecha) =>
        Resolver(RutaSalida ?? throw new ConfiguracionInvalidaException($"Fuente {IdFuente}: no tiene RutaSalida."), fecha);

    /// <summary>Nombre del unificado para la fecha, p. ej. <c>Cartera_ISB_18112026.xlsx</c>.</summary>
    public string ResolverNombreSalida(DateOnly fecha) =>
        Resolver(NombreSalida ?? throw new ConfiguracionInvalidaException($"Fuente {IdFuente}: no tiene NombreSalida."), fecha);

    /// <summary>
    /// {MES} = nombre del mes en mayúsculas (NOVIEMBRE); cualquier otro {formato} = la fecha con ese formato.
    /// </summary>
    public static string Resolver(string texto, DateOnly fecha) =>
        MarcadorFecha().Replace(texto, m => m.Groups[1].Value == "MES"
            ? fecha.ToString("MMMM", Espanol).ToUpperInvariant()
            : fecha.ToString(m.Groups[1].Value, CultureInfo.InvariantCulture));

    private string Requerido(string valor, string campo) =>
        string.IsNullOrWhiteSpace(valor)
            ? throw new ConfiguracionInvalidaException($"Fuente {IdFuente}: {campo} es obligatorio.")
            : valor.Trim();

    [GeneratedRegex(@"\{([^{}]+)\}")]
    private static partial Regex MarcadorFecha();
}
