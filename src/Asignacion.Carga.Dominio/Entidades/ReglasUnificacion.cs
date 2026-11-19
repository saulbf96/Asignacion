namespace Asignacion.Carga.Dominio.Entidades;

/// <summary>
/// Reglas que se aplican al unificar los archivos de una fuente (p. ej. LEGAL replica el Power Query
/// DETALLE_CARTERA_V2 que WFM usaba a mano). Todas son opcionales: sin reglas, el unificado sale tal cual.
/// </summary>
/// <param name="ColumnasExcluidas">Columnas de la descarga que no pasan al unificado (p. ej. TIPO_QUEJA).</param>
/// <param name="FilasExcluidas">Filas que se descartan según el valor de una columna (p. ej. CAMPANIA que empieza con QUEJA o CLON).</param>
/// <param name="Reemplazos">Valores exactos que se cambian por otro (p. ej. "N/A" por "0" en TELEFONO1..4).</param>
/// <param name="Tipos">Conversiones de tipo, como el "Tipo cambiado" del Power Query: si el valor no se puede convertir, número = 0 y fecha = vacío.</param>
/// <param name="ColumnasNuevas">Columnas que se agregan (valor fijo, copia de otra o en blanco), en orden, después de otra columna.</param>
/// <param name="Grupos">Grupos de archivos (p. ej. SCL y SMART) que se reconocen por el nombre de carpeta o zip; cada uno puede renombrar columnas. Se procesan en este orden.</param>
/// <param name="ColumnasDuplicado">
/// Columnas que identifican una fila repetida (p. ej. CLIENTE_UNICO): se deja la primera, respetando el orden de los grupos.
/// Vacío = la fila se considera repetida solo si TODOS sus campos son iguales.
/// </param>
public sealed record ReglasUnificacion(
    IReadOnlyList<string> ColumnasExcluidas,
    IReadOnlyList<FiltroFilas> FilasExcluidas,
    IReadOnlyList<ReemplazoValor> Reemplazos,
    IReadOnlyList<ConversionTipo> Tipos,
    IReadOnlyList<ColumnaNueva> ColumnasNuevas,
    IReadOnlyList<GrupoArchivos> Grupos,
    IReadOnlyList<string> ColumnasDuplicado)
{
    public static ReglasUnificacion Ninguna { get; } = new([], [], [], [], [], [], []);
}

public enum TipoColumna
{
    /// <summary>Texto: el valor no cambia, pero en el Excel se escribe como texto aunque parezca número (p. ej. GENERO_CLIENTE "1").</summary>
    Texto,
    /// <summary>Número entero (Int64 de Power Query): decimales se redondean; lo que no es número = 0.</summary>
    Entero,
    /// <summary>Número con decimales: lo que no es número = 0.</summary>
    Decimal,
    /// <summary>Fecha dd/MM/yyyy: lo que no es fecha (o es anterior a 1900, que Excel no maneja) = vacío.</summary>
    Fecha
}

/// <summary>Convierte <paramref name="Columnas"/> al <paramref name="Tipo"/> indicado.</summary>
public sealed record ConversionTipo(IReadOnlyList<string> Columnas, TipoColumna Tipo);

/// <summary>Descarta la fila si <paramref name="Columna"/> empieza con alguno de los textos (distingue mayúsculas, como Power Query).</summary>
public sealed record FiltroFilas(string Columna, IReadOnlyList<string> EmpiezaCon);

/// <summary>En <paramref name="Columnas"/>, el valor exactamente igual a <paramref name="Valor"/> se cambia por <paramref name="Por"/>.</summary>
public sealed record ReemplazoValor(IReadOnlyList<string> Columnas, string Valor, string? Por);

/// <summary>
/// Columna agregada después de <paramref name="DespuesDe"/>. Lleva <paramref name="Valor"/> fijo, o la copia de
/// <paramref name="CopiaDe"/> (antes de renombrar), o va en blanco si no trae ninguno.
/// <paramref name="Resaltar"/> pinta el encabezado (las que el usuario llena a mano).
/// </summary>
public sealed record ColumnaNueva(string Nombre, string DespuesDe, string? Valor = null, string? CopiaDe = null, bool Resaltar = false)
{
    public bool EnBlanco => Valor is null && CopiaDe is null;
}

/// <summary>
/// Grupo de archivos: un archivo pertenece al grupo si alguna carpeta o zip de su ruta coincide con un patrón
/// (comodines * y ?, sin distinguir mayúsculas). <paramref name="Renombrar"/>: en este grupo, el dato de la columna
/// (clave) se pasa a otra columna (valor) que va al final del unificado, y la original queda en blanco.
/// </summary>
public sealed record GrupoArchivos(string Nombre, IReadOnlyList<string> Patrones, IReadOnlyDictionary<string, string> Renombrar);
