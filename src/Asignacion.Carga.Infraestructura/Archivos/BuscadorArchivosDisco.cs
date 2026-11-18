using Asignacion.Carga.Aplicacion.Interfaces;
using Asignacion.Carga.Dominio.Entidades;

namespace Asignacion.Carga.Infraestructura.Archivos;

/// <summary>
/// Busca los archivos del día en una carpeta local o de red (ruta UNC).
/// Solo lee: nunca mueve ni borra archivos de la carpeta de origen.
/// </summary>
internal sealed class BuscadorArchivosDisco : IBuscadorArchivos
{
    private static readonly EnumerationOptions Opciones = new()
    {
        MatchType = MatchType.Simple,          // evita que "*.xlsx" coincida con nombres cortos 8.3
        MatchCasing = MatchCasing.CaseInsensitive,
        RecurseSubdirectories = false,
        IgnoreInaccessible = true
    };

    public Task<IReadOnlyList<ArchivoOrigen>> BuscarAsync(ConfiguracionFuente fuente, DateOnly fecha, CancellationToken ct) =>
        Task.Run<IReadOnlyList<ArchivoOrigen>>(() =>
        {
            if (!Directory.Exists(fuente.RutaOrigen))
                throw new DirectoryNotFoundException(
                    $"No se puede acceder a la ruta de origen '{fuente.RutaOrigen}'. Verifique que exista y que el usuario que ejecuta tenga permisos.");

            var patron = fuente.ResolverPatron(fecha);
            return new DirectoryInfo(fuente.RutaOrigen)
                .EnumerateFiles(patron, Opciones)
                .Where(f => !ArchivoOrigen.EsTemporalDeOffice(f.Name))
                .Select(f => new ArchivoOrigen(f.Name, f.DirectoryName ?? fuente.RutaOrigen, fecha, f.LastWriteTime))
                .ToList();
        }, ct);
}
