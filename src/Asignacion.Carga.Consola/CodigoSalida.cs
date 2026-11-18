using Asignacion.Carga.Aplicacion.CasosUso;
using Asignacion.Carga.Dominio.Enums;

namespace Asignacion.Carga.Consola;

/// <summary>
/// Código con el que termina el proceso; el job lo usa para saber si falló.
/// </summary>
public static class CodigoSalida
{
    public const int Correcto = 0;
    public const int Error = 1;
    public const int ParametrosInvalidos = 2;
    public const int SinArchivo = 3;

    /// <summary>Un error en cualquier fuente gana; luego "sin archivo"; si no, correcto.</summary>
    public static int Desde(IReadOnlyList<ResultadoProceso> resultados)
    {
        if (resultados.Any(r => r.FueError)) return Error;
        if (resultados.Any(r => r.Estatus == EstatusCarga.SinArchivo)) return SinArchivo;
        return Correcto;
    }
}
