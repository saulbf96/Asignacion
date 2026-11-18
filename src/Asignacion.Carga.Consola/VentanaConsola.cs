using System.Runtime.InteropServices;

namespace Asignacion.Carga.Consola;

/// <summary>
/// Detalles de la ventana negra: el mensaje final y la pausa al terminar.
/// La pausa solo ocurre si el programa se abrió con doble clic (la ventana es solo suya);
/// si se ejecuta desde una consola, un .cmd o un job, nunca espera.
/// </summary>
internal static partial class VentanaConsola
{
    public static string MensajeFinal(int codigo) => codigo switch
    {
        CodigoSalida.Correcto => "Terminó bien (código 0).",
        CodigoSalida.Error => "Hubo un ERROR (código 1). Revise su correo y la carpeta logs.",
        CodigoSalida.ParametrosInvalidos => "Parámetros inválidos (código 2). Use --ayuda.",
        CodigoSalida.SinArchivo => "No hubo archivo del día (código 3).",
        _ => $"Terminó con código {codigo}."
    };

    /// <summary>true si la ventana se abrió solo para este programa (doble clic en el .exe).</summary>
    public static bool SeAbrioConDobleClic()
    {
        if (!OperatingSystem.IsWindows() || Console.IsInputRedirected || Console.IsOutputRedirected) return false;
        try
        {
            var procesos = new uint[2];
            return GetConsoleProcessList(procesos, (uint)procesos.Length) == 1;
        }
        catch
        {
            return false;
        }
    }

    public static void EsperarTecla()
    {
        try
        {
            Console.WriteLine();
            Console.Write("Presione cualquier tecla para cerrar...");
            Console.ReadKey(intercept: true);
        }
        catch (InvalidOperationException)
        {
            // sin teclado disponible: no se espera
        }
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial uint GetConsoleProcessList([Out] uint[] procesos, uint cantidad);
}
