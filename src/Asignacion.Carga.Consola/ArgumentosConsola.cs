using System.Globalization;
using Asignacion.Carga.Aplicacion.CasosUso;

namespace Asignacion.Carga.Consola;

/// <summary>
/// Parámetros de la línea de comandos:
///   --fuente N          solo esa fuente (por omisión, todas las activas)
///   --fecha yyyy-MM-dd  día del archivo a buscar (por omisión, hoy)
///   --forzar            recarga aunque el archivo ya esté cargado
///   --ambiente NOMBRE   usa appsettings.NOMBRE.json (si no se indica: variable ASIGNACION_AMBIENTE
///                       o "Ambiente" del appsettings.json; si tampoco, Local)
///   --sin-pausa         no espera una tecla al terminar (para jobs)
///   --ayuda             muestra la ayuda
/// </summary>
/// <param name="Ambiente">Null = no se indicó por parámetro ni variable de entorno.</param>
public sealed record ArgumentosConsola(SolicitudCarga Solicitud, string? Ambiente, bool MostrarAyuda, bool SinPausa = false)
{
    public const string AmbientePorOmision = "Local";

    public const string Ayuda = """
        Uso: Asignacion.Carga.exe [opciones]

          --fuente N          Procesa solo la fuente N (por omisión: todas las activas)
          --fecha yyyy-MM-dd  Día del archivo a buscar (por omisión: hoy)
          --forzar            Vuelve a cargar aunque el archivo ya esté cargado
          --ambiente NOMBRE   Usa appsettings.NOMBRE.json (por omisión: el "Ambiente" del appsettings.json)
          --sin-pausa         No espera una tecla al terminar (usar en jobs / tareas programadas)
          --ayuda             Muestra esta ayuda

        Códigos de salida:
          0  Todo bien (cargado, o ya estaba cargado)
          1  Hubo un error en alguna fuente
          2  Parámetros inválidos
          3  No hubo archivo del día
        """;

    public static bool TryParse(IReadOnlyList<string> args, out ArgumentosConsola resultado, out string? error)
    {
        int? fuente = null;
        DateOnly? fecha = null;
        var forzar = false;
        var ayuda = false;
        var sinPausa = false;
        var ambiente = Environment.GetEnvironmentVariable("ASIGNACION_AMBIENTE");
        resultado = null!;
        error = null;

        for (var i = 0; i < args.Count; i++)
        {
            var opcion = args[i].Trim().ToLowerInvariant();
            switch (opcion)
            {
                case "--fuente":
                    if (!Siguiente(args, ref i, out var f) || !int.TryParse(f, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n <= 0)
                        return Falla("--fuente requiere un número mayor a 0.", out error);
                    fuente = n;
                    break;
                case "--fecha":
                    if (!Siguiente(args, ref i, out var d) || !DateOnly.TryParseExact(d, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dia))
                        return Falla("--fecha requiere el formato yyyy-MM-dd (p. ej. 2026-11-18).", out error);
                    fecha = dia;
                    break;
                case "--ambiente":
                    if (!Siguiente(args, ref i, out var a) || string.IsNullOrWhiteSpace(a))
                        return Falla("--ambiente requiere un nombre (p. ej. Local o Pruebas).", out error);
                    ambiente = a.Trim();
                    break;
                case "--forzar":
                    forzar = true;
                    break;
                case "--sin-pausa":
                    sinPausa = true;
                    break;
                case "--ayuda" or "-h" or "--help" or "/?":
                    ayuda = true;
                    break;
                default:
                    return Falla($"Opción desconocida: '{args[i]}'. Use --ayuda.", out error);
            }
        }

        resultado = new ArgumentosConsola(new SolicitudCarga(fuente, fecha, forzar), ambiente, ayuda, sinPausa);
        return true;
    }

    private static bool Siguiente(IReadOnlyList<string> args, ref int i, out string valor)
    {
        if (i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            valor = args[++i];
            return true;
        }
        valor = string.Empty;
        return false;
    }

    private static bool Falla(string mensaje, out string? error)
    {
        error = mensaje;
        return false;
    }
}
