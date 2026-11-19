using Asignacion.Carga.Dominio.Entidades;

namespace Asignacion.Carga.Infraestructura.Archivos;

/// <summary>
/// Sección "Unificacion" del appsettings. La contraseña de los zip NUNCA va aquí:
/// se lee de la variable de entorno indicada en <see cref="VariableContrasena"/>.
/// </summary>
public sealed class OpcionesUnificacion
{
    public const string Seccion = "Unificacion";
    public const string VariableContrasenaPorOmision = "ASIGNACION_ZIP_PASSWORD";

    /// <summary>Variable de entorno con la contraseña de los zip cifrados.</summary>
    public string VariableContrasena { get; set; } = VariableContrasenaPorOmision;

    /// <summary>
    /// Solo para ambientes de prueba: contraseña de los zip escrita en el appsettings (archivo fuera de git).
    /// Si la variable de entorno existe, tiene prioridad.
    /// </summary>
    public string Contrasena { get; set; } = string.Empty;

    /// <summary>Carpeta de trabajo; vacío = carpeta temporal de Windows.</summary>
    public string CarpetaTemporal { get; set; } = string.Empty;

    /// <summary>Reglas de unificación por nombre de fuente (p. ej. "LEGAL"). Una fuente sin reglas se unifica tal cual.</summary>
    public Dictionary<string, OpcionesReglas> Reglas { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public ReglasUnificacion ReglasDe(string fuente) =>
        Reglas.TryGetValue(fuente, out var r) ? r.ALasReglas() : ReglasUnificacion.Ninguna;
}

/// <summary>Reglas de una fuente tal como se escriben en el appsettings (ver <see cref="ReglasUnificacion"/>).</summary>
public sealed class OpcionesReglas
{
    public List<string> ColumnasExcluidas { get; set; } = [];
    public List<OpcionesFiltro> FilasExcluidas { get; set; } = [];
    public List<OpcionesReemplazo> Reemplazos { get; set; } = [];
    public List<OpcionesTipo> Tipos { get; set; } = [];
    public List<OpcionesColumnaNueva> ColumnasNuevas { get; set; } = [];
    public List<OpcionesGrupo> Grupos { get; set; } = [];
    public List<string> ColumnasDuplicado { get; set; } = [];

    public ReglasUnificacion ALasReglas() => new(
        ColumnasExcluidas,
        [.. FilasExcluidas.Select(f => new FiltroFilas(f.Columna, f.EmpiezaCon))],
        [.. Reemplazos.Select(r => new ReemplazoValor(r.Columnas, r.Valor, string.IsNullOrEmpty(r.Por) ? null : r.Por))],
        [.. Tipos.Select(t => new ConversionTipo(t.Columnas, t.Tipo))],
        [.. ColumnasNuevas.Select(c => new ColumnaNueva(c.Nombre, c.DespuesDe, c.Valor, c.CopiaDe, c.Resaltar))],
        [.. Grupos.Select(g => new GrupoArchivos(g.Nombre, g.Patrones, new Dictionary<string, string>(g.Renombrar, StringComparer.Ordinal)))],
        ColumnasDuplicado);
}

public sealed class OpcionesFiltro
{
    public string Columna { get; set; } = string.Empty;
    public List<string> EmpiezaCon { get; set; } = [];
}

public sealed class OpcionesReemplazo
{
    public List<string> Columnas { get; set; } = [];
    public string Valor { get; set; } = string.Empty;
    public string? Por { get; set; }
}

public sealed class OpcionesTipo
{
    public List<string> Columnas { get; set; } = [];
    public TipoColumna Tipo { get; set; }
}

public sealed class OpcionesColumnaNueva
{
    public string Nombre { get; set; } = string.Empty;
    public string DespuesDe { get; set; } = string.Empty;
    public string? Valor { get; set; }
    public string? CopiaDe { get; set; }
    public bool Resaltar { get; set; }
}

public sealed class OpcionesGrupo
{
    public string Nombre { get; set; } = string.Empty;
    public List<string> Patrones { get; set; } = [];
    public Dictionary<string, string> Renombrar { get; set; } = [];
}
