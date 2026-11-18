using Asignacion.Carga.Dominio.Excepciones;

namespace Asignacion.Carga.Dominio.Servicios;

/// <summary>
/// Verifica que el archivo traiga todos los encabezados esperados.
/// La comparación ignora mayúsculas/minúsculas y espacios al inicio o al final.
/// Encabezados de más no son error (se ignoran al insertar).
/// </summary>
public static class ValidadorEncabezados
{
    public static IReadOnlyList<string> ObtenerFaltantes(
        IEnumerable<string> esperados, IEnumerable<string?> encontrados)
    {
        var presentes = new HashSet<string>(
            encontrados.Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e!.Trim()),
            StringComparer.OrdinalIgnoreCase);

        return esperados
            .Select(e => e.Trim())
            .Where(e => !presentes.Contains(e))
            .ToList();
    }

    /// <exception cref="EncabezadosInvalidosException">Si falta al menos un encabezado.</exception>
    public static void Validar(string archivo, IEnumerable<string> esperados, IEnumerable<string?> encontrados)
    {
        var faltantes = ObtenerFaltantes(esperados, encontrados);
        if (faltantes.Count > 0)
            throw new EncabezadosInvalidosException(archivo, faltantes);
    }
}
