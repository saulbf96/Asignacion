using System.Security.Cryptography;
using System.Text;

namespace Asignacion.Carga.Dominio.Servicios;

/// <summary>
/// Regla de negocio (WFM, 19/11/2026): si una fila es idéntica en TODOS sus campos a otra ya tomada
/// (p. ej. la misma cuenta en dos segmentos), se deja una sola. Si cambia aunque sea un campo, se quedan las dos.
/// Guarda solo un hash por fila para no tener en memoria todo el contenido.
/// </summary>
public sealed class DetectorFilasRepetidas
{
    private const char SeparadorCampos = '\u001F'; // no aparece en los datos: evita que "a|bc" y "ab|c" coincidan
    private readonly HashSet<string> _vistas = new(StringComparer.Ordinal);

    /// <summary>Filas descartadas por repetidas.</summary>
    public int Repetidas { get; private set; }

    /// <summary>true si la fila es la primera con ese contenido (se toma); false si es repetida (se descarta).</summary>
    public bool EsNueva(IReadOnlyList<string?> campos)
    {
        var texto = new StringBuilder();
        foreach (var campo in campos)
            texto.Append(campo).Append(SeparadorCampos); // null y vacío cuentan igual: en Excel son la misma celda vacía

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto.ToString())));
        if (_vistas.Add(hash)) return true;
        Repetidas++;
        return false;
    }
}
