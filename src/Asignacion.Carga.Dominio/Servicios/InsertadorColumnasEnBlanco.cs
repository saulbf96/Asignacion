using Asignacion.Carga.Dominio.Excepciones;

namespace Asignacion.Carga.Dominio.Servicios;

/// <summary>
/// Agrega al archivo de salida columnas que no vienen en la entrada, solo con su encabezado y en blanco,
/// después de una columna dada (p. ej. NUEVA/REASIGNADA, TIPO_ASIG... después de CLIENTE_UNICO).
/// Si alguna ya viene en la entrada, no se duplica: se respeta la de la entrada.
/// </summary>
public sealed class InsertadorColumnasEnBlanco
{
    private readonly int _posicion;   // índice en la entrada donde se insertan (después de "DespuesDe")
    private readonly int _cantidad;

    public InsertadorColumnasEnBlanco(IReadOnlyList<string> encabezados, string? despuesDe, IEnumerable<string>? columnas)
    {
        var existentes = new HashSet<string>(encabezados.Select(e => e.Trim()), StringComparer.Ordinal);
        var nuevas = (columnas ?? []).Select(c => c.Trim()).Where(c => c.Length > 0 && !existentes.Contains(c)).Distinct(StringComparer.Ordinal).ToArray();

        Encabezados = [.. encabezados];
        if (nuevas.Length == 0 || string.IsNullOrWhiteSpace(despuesDe)) return;

        var indice = Encabezados.FindIndex(e => string.Equals(e.Trim(), despuesDe.Trim(), StringComparison.Ordinal));
        if (indice < 0)
            throw new DominioException($"No se pueden agregar las columnas en blanco: el archivo no trae la columna '{despuesDe}'.");

        _posicion = indice + 1;
        _cantidad = nuevas.Length;
        Encabezados.InsertRange(_posicion, nuevas);
    }

    /// <summary>Encabezados de salida (los de la entrada con las columnas en blanco ya insertadas).</summary>
    public List<string> Encabezados { get; }

    /// <summary>Columnas que se agregaron (0 si no aplica).</summary>
    public int Agregadas => _cantidad;

    /// <summary>La fila con los campos vacíos insertados en su lugar.</summary>
    public string?[] Aplicar(string?[] campos)
    {
        if (_cantidad == 0) return campos;
        var salida = new string?[campos.Length + _cantidad];
        Array.Copy(campos, 0, salida, 0, _posicion);
        Array.Copy(campos, _posicion, salida, _posicion + _cantidad, campos.Length - _posicion);
        return salida;
    }
}
