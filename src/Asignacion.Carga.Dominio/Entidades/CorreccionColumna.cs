using Asignacion.Carga.Dominio.Excepciones;

namespace Asignacion.Carga.Dominio.Entidades;

/// <summary>
/// Regla "ORIGEN>DESTINO": si ORIGEN trae dato y DESTINO está vacío,
/// el dato pasa a DESTINO y ORIGEN queda en blanco (p. ej. TIPO_GESTION_>GESTORES).
/// </summary>
public sealed record CorreccionColumna(string Origen, string Destino)
{
    /// <summary>Lee "A>B;C>D". Vacío o null = sin correcciones.</summary>
    public static IReadOnlyList<CorreccionColumna> Leer(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return [];

        var lista = new List<CorreccionColumna>();
        foreach (var parte in texto.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var lados = parte.Split('>', StringSplitOptions.TrimEntries);
            if (lados.Length != 2 || lados[0].Length == 0 || lados[1].Length == 0 || lados[0] == lados[1])
                throw new ConfiguracionInvalidaException($"Corrección de columnas inválida: '{parte}'. Use ORIGEN>DESTINO.");
            lista.Add(new CorreccionColumna(lados[0], lados[1]));
        }
        return lista;
    }
}
