using System.Text.RegularExpressions;
using Asignacion.Carga.Dominio.Entidades;
using Asignacion.Carga.Dominio.Excepciones;

namespace Asignacion.Carga.Dominio.Servicios;

/// <summary>
/// Decide a qué grupo (p. ej. SCL o SMART) pertenece cada Excel según las carpetas o zip de su ruta.
/// Si hay grupos configurados, todo archivo debe caer en exactamente uno: si no, se detiene la unificación
/// (es preferible a cargar una cartera mal armada).
/// </summary>
public sealed class ClasificadorGrupos
{
    private readonly (GrupoArchivos Grupo, Regex[] Patrones)[] _grupos;

    public ClasificadorGrupos(IReadOnlyList<GrupoArchivos> grupos)
    {
        _grupos = grupos.Select(g => (g, g.Patrones.Select(Comodin).ToArray())).ToArray();
    }

    public bool HayGrupos => _grupos.Length > 0;

    /// <summary>Posición del grupo en la configuración (los grupos se procesan en ese orden).</summary>
    public int Orden(GrupoArchivos? grupo) => grupo is null ? 0 : Array.FindIndex(_grupos, g => ReferenceEquals(g.Grupo, grupo));

    /// <summary>
    /// Grupo del archivo, a partir de su ruta relativa a la entrada (carpetas y zip de origen; el nombre del Excel no cuenta).
    /// Devuelve null si no hay grupos configurados.
    /// </summary>
    /// <exception cref="DominioException">Si no coincide con ningún grupo o coincide con más de uno.</exception>
    public GrupoArchivos? Clasificar(string rutaRelativa)
    {
        if (_grupos.Length == 0) return null;

        var carpetas = rutaRelativa.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries).SkipLast(1).ToArray();
        var coinciden = _grupos.Where(g => carpetas.Any(c => g.Patrones.Any(p => p.IsMatch(c)))).Select(g => g.Grupo).ToList();

        return coinciden.Count switch
        {
            1 => coinciden[0],
            0 => throw new DominioException(
                $"No se sabe a qué grupo ({string.Join(" o ", _grupos.Select(g => g.Grupo.Nombre))}) pertenece '{rutaRelativa}'. " +
                $"Póngalo en una carpeta con el nombre del grupo (p. ej. {string.Join(", ", _grupos.Select(g => g.Grupo.Nombre.ToLowerInvariant()))}) o deje el zip con su nombre original. No se unificó nada."),
            _ => throw new DominioException(
                $"'{rutaRelativa}' coincide con más de un grupo ({string.Join(", ", coinciden.Select(g => g.Nombre))}). No se unificó nada.")
        };
    }

    private static Regex Comodin(string patron) => new(
        "^" + Regex.Escape(patron.Trim()).Replace(@"\*", ".*").Replace(@"\?", ".") + "$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
