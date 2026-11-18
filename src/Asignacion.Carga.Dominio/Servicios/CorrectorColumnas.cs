using Asignacion.Carga.Dominio.Entidades;

namespace Asignacion.Carga.Dominio.Servicios;

/// <summary>
/// Aplica las correcciones de columnas a una fila ya separada en campos.
/// Las correcciones cuyas columnas no existen en el archivo se ignoran.
/// </summary>
public sealed class CorrectorColumnas
{
    private readonly (int Origen, int Destino)[] _indices;

    public CorrectorColumnas(IReadOnlyList<string> encabezados, IEnumerable<CorreccionColumna> correcciones)
    {
        var posicion = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < encabezados.Count; i++)
            posicion.TryAdd(encabezados[i].Trim(), i);

        _indices = correcciones
            .Where(c => posicion.ContainsKey(c.Origen) && posicion.ContainsKey(c.Destino))
            .Select(c => (posicion[c.Origen], posicion[c.Destino]))
            .ToArray();
    }

    /// <summary>true si hay al menos una corrección que aplica a este archivo.</summary>
    public bool TieneCorrecciones => _indices.Length > 0;

    /// <summary>Corrige la fila en el mismo arreglo. Devuelve cuántos datos se movieron.</summary>
    public int Aplicar(string?[] campos)
    {
        var movidos = 0;
        foreach (var (origen, destino) in _indices)
        {
            if (origen >= campos.Length || destino >= campos.Length) continue;
            if (!string.IsNullOrWhiteSpace(campos[origen]) && string.IsNullOrWhiteSpace(campos[destino]))
            {
                campos[destino] = campos[origen];
                campos[origen] = null;
                movidos++;
            }
        }
        return movidos;
    }
}
