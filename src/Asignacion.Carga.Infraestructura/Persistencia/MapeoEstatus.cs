using Asignacion.Carga.Dominio.Enums;

namespace Asignacion.Carga.Infraestructura.Persistencia;

/// <summary>
/// Traduce el estatus del dominio al código que guarda la columna BITACORA_CARGA.ESTATUS.
/// </summary>
internal static class MapeoEstatus
{
    private static readonly Dictionary<EstatusCarga, string> ACodigo = new()
    {
        [EstatusCarga.EnProceso] = "EN_PROCESO",
        [EstatusCarga.Exitoso] = "EXITOSO",
        [EstatusCarga.Error] = "ERROR",
        [EstatusCarga.Reemplazado] = "REEMPLAZADO",
        [EstatusCarga.Omitido] = "OMITIDO",
        [EstatusCarga.SinArchivo] = "SIN_ARCHIVO",
    };

    private static readonly Dictionary<string, EstatusCarga> AEstatus =
        ACodigo.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

    public static string Codigo(EstatusCarga estatus) => ACodigo[estatus];

    public static EstatusCarga Estatus(string codigo) =>
        AEstatus.TryGetValue(codigo.Trim(), out var e)
            ? e
            : throw new InvalidOperationException($"Estatus de bitácora desconocido: '{codigo}'.");
}
