namespace Asignacion.Carga.Dominio.Excepciones;

/// <summary>
/// El archivo no trae los encabezados esperados (cambió su estructura).
/// </summary>
public sealed class EncabezadosInvalidosException : DominioException
{
    public IReadOnlyList<string> Faltantes { get; }

    public EncabezadosInvalidosException(string archivo, IReadOnlyList<string> faltantes)
        : base($"El archivo '{archivo}' no trae los encabezados esperados. Faltan: {string.Join(", ", faltantes)}.")
    {
        Faltantes = faltantes;
    }
}
