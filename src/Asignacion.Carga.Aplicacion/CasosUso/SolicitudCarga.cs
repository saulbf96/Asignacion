namespace Asignacion.Carga.Aplicacion.CasosUso;

/// <summary>
/// Parámetros de una ejecución de la carga.
/// </summary>
/// <param name="IdFuente">Fuente a procesar; null = todas las activas.</param>
/// <param name="Fecha">Día del archivo a buscar; null = hoy.</param>
/// <param name="Forzar">Recargar aunque el archivo ya esté cargado con éxito.</param>
public sealed record SolicitudCarga(int? IdFuente = null, DateOnly? Fecha = null, bool Forzar = false);
