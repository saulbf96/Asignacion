namespace Asignacion.Carga.Dominio.Entidades;

/// <summary>
/// Resultado de unificar lo que dejó el usuario en la carpeta de entrada.
/// </summary>
/// <param name="Lectura">Archivo unificado temporal, del que se leen las filas para insertar.</param>
/// <param name="Destino">Dónde debe quedar publicado (carpeta del mes y nombre final). Es lo que se registra en la bitácora.</param>
/// <param name="Hoja">Hoja del archivo unificado.</param>
/// <param name="Entradas">Lo que había en la carpeta de entrada (zip, carpetas, archivos) y se borrará al terminar.</param>
/// <param name="ArchivosUnificados">Excel distintos que se unieron.</param>
/// <param name="ArchivosRepetidos">Excel idénticos encontrados más de una vez (p. ej. en el zip y en la carpeta) que se tomaron una sola vez.</param>
/// <param name="Filas">Filas de datos del unificado.</param>
/// <param name="CarpetaTemporal">Carpeta de trabajo que se borra al final.</param>
public sealed record ArchivoUnificado(
    ArchivoOrigen Lectura,
    ArchivoOrigen Destino,
    string Hoja,
    IReadOnlyList<string> Entradas,
    int ArchivosUnificados,
    int ArchivosRepetidos,
    int Filas,
    string CarpetaTemporal)
{
    /// <summary>El unificado siempre lleva los encabezados en la fila 1.</summary>
    public const int FilaEncabezado = 1;
}
