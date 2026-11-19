using System.Security.Cryptography;
using System.Text;

namespace Asignacion.Carga.Dominio.Servicios;

/// <summary>
/// Descarta filas repetidas y deja la primera (por eso el orden de los archivos importa: p. ej. SCL antes que SMART).
/// - Con columnas clave (p. ej. CLIENTE_UNICO, como el Power Query de WFM): repetida = misma clave.
/// - Sin columnas clave: repetida = TODOS los campos iguales.
/// Guarda solo un hash por fila para no tener en memoria todo el contenido.
/// </summary>
public sealed class DetectorFilasRepetidas
{
    private const char SeparadorCampos = '\u001F'; // no aparece en los datos: evita que "a|bc" y "ab|c" coincidan
    private readonly HashSet<string> _vistas = new(StringComparer.Ordinal);
    private readonly int[] _clave;

    /// <param name="columnasClave">Posiciones de las columnas que identifican la fila; vacío o null = todas.</param>
    public DetectorFilasRepetidas(IReadOnlyList<int>? columnasClave = null)
    {
        _clave = columnasClave?.ToArray() ?? [];
    }

    /// <summary>Filas descartadas por repetidas.</summary>
    public int Repetidas { get; private set; }

    /// <summary>true si la fila es la primera con esa clave (se toma); false si es repetida (se descarta).</summary>
    public bool EsNueva(IReadOnlyList<string?> campos)
    {
        var texto = new StringBuilder();
        if (_clave.Length == 0)
            foreach (var campo in campos) texto.Append(campo).Append(SeparadorCampos); // null y vacío cuentan igual
        else
            foreach (var i in _clave) texto.Append(i < campos.Count ? campos[i] : null).Append(SeparadorCampos);

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto.ToString())));
        if (_vistas.Add(hash)) return true;
        Repetidas++;
        return false;
    }
}
