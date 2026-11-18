using System.Globalization;
using System.Text;
using Asignacion.Carga.Dominio.Entidades;
using Asignacion.Carga.Dominio.Enums;

namespace Asignacion.Carga.Aplicacion.Notificaciones;

/// <summary>
/// Arma el asunto y el cuerpo de cada notificación. Solo da formato: no envía nada.
/// </summary>
public sealed class GeneradorNotificaciones
{
    private static readonly CultureInfo Mx = CultureInfo.GetCultureInfo("es-MX");

    public Notificacion SinArchivo(ConfiguracionFuente fuente, DateOnly fecha, string patron) =>
        Crear(TipoNotificacion.SinArchivo, fuente, $"Sin archivo del {Fecha(fecha)}",
            ("Resultado", "No se encontró archivo del día. No se cargó nada."),
            ("Ruta", fuente.RutaOrigen),
            ("Patrón buscado", patron));

    public Notificacion Omitido(ConfiguracionFuente fuente, ArchivoOrigen archivo, InicioCarga inicio) =>
        Crear(TipoNotificacion.Omitido, fuente, $"{archivo.Nombre} ya estaba cargado",
            ("Resultado", "Se volvió a ejecutar el proceso, pero el archivo ya se había cargado con éxito. No se cargó nada."),
            ("Archivo", archivo.Nombre),
            ("Cargado previamente", inicio.FechaCargaPrevia is { } f ? FechaHora(f) : "-"),
            ("Filas de esa carga", Numero(inicio.FilasCargaPrevia)));

    public Notificacion Inicio(ConfiguracionFuente fuente, ArchivoOrigen archivo, InicioCarga inicio, int archivosEncontrados)
    {
        var datos = new List<(string, string)>
        {
            ("Archivo", archivo.Nombre),
            ("Ruta", archivo.Carpeta),
            ("Id de carga", inicio.IdCarga.ToString(CultureInfo.InvariantCulture)),
        };
        if (inicio.FilasAnterioresEliminadas > 0)
            datos.Add(("Recarga forzada", $"se reemplazan {Numero(inicio.FilasAnterioresEliminadas)} filas de la carga anterior"));
        if (archivosEncontrados > 1)
            datos.Add(("Aviso", $"había {archivosEncontrados} archivos del día; se tomó el más reciente"));

        return Crear(TipoNotificacion.Inicio, fuente, $"Inició la carga de {archivo.Nombre}", [.. datos]);
    }

    public Notificacion Exitoso(ConfiguracionFuente fuente, ResultadoCarga r)
    {
        var datos = new List<(string, string)>
        {
            ("Resultado", r.EstaCompleta ? "Carga terminada correctamente." : "Carga terminada, pero NO coinciden las filas leídas con las insertadas."),
            ("Archivo", r.ArchivoOrigen),
            ("Filas leídas", Numero(r.FilasLeidas)),
            ("Filas insertadas", Numero(r.FilasInsertadas)),
            ("Duración", Duracion(r.Duracion)),
            ("Id de carga", r.IdCarga.ToString(CultureInfo.InvariantCulture)),
        };
        var asunto = r.EstaCompleta ? $"Terminó la carga de {r.ArchivoOrigen}" : $"Revisar: carga incompleta de {r.ArchivoOrigen}";
        return Crear(TipoNotificacion.Exitoso, fuente, asunto, [.. datos]);
    }

    public Notificacion InicioUnificado(ConfiguracionFuente fuente, ArchivoUnificado u, InicioCarga inicio)
    {
        var datos = new List<(string, string)>
        {
            ("Archivo", u.Destino.Nombre),
            ("Entrada", $"{fuente.RutaOrigen} ({u.Entradas.Count} elemento(s): {string.Join(", ", u.Entradas.Select(Path.GetFileName))})"),
            ("Archivos unificados", Numero(u.ArchivosUnificados)),
            ("Filas unificadas", Numero(u.Filas)),
            ("Id de carga", inicio.IdCarga.ToString(CultureInfo.InvariantCulture)),
        };
        if (u.ArchivosRepetidos > 0)
            datos.Add(("Aviso", $"{u.ArchivosRepetidos} archivo(s) venían repetidos (p. ej. en el zip y en la carpeta); se tomaron una sola vez"));
        if (inicio.FilasAnterioresEliminadas > 0)
            datos.Add(("Recarga", $"se reemplazan {Numero(inicio.FilasAnterioresEliminadas)} filas de la carga anterior de este archivo"));

        return Crear(TipoNotificacion.Inicio, fuente, $"Inició la carga de {u.Destino.Nombre}", [.. datos]);
    }

    public Notificacion ExitosoUnificado(
        ConfiguracionFuente fuente, ResultadoCarga r, ArchivoUnificado u, bool borrarEntrada, IReadOnlyList<string> noBorrados)
    {
        var entrada = !borrarEntrada ? "no se vacía (BORRAR_ENTRADA = 0)"
            : noBorrados.Count == 0 ? "vaciada"
            : $"NO se pudo borrar: {string.Join(", ", noBorrados.Select(Path.GetFileName))}";

        var datos = new List<(string, string)>
        {
            ("Resultado", r.EstaCompleta ? "Carga terminada correctamente." : "Carga terminada, pero NO coinciden las filas leídas con las insertadas."),
            ("Archivo", r.ArchivoOrigen),
            ("Guardado en", u.Destino.RutaCompleta),
            ("Archivos unificados", Numero(u.ArchivosUnificados)),
            ("Filas leídas", Numero(r.FilasLeidas)),
            ("Filas insertadas", Numero(r.FilasInsertadas)),
            ("Carpeta de entrada", entrada),
            ("Duración", Duracion(r.Duracion)),
            ("Id de carga", r.IdCarga.ToString(CultureInfo.InvariantCulture)),
        };
        var asunto = !r.EstaCompleta ? $"Revisar: carga incompleta de {r.ArchivoOrigen}"
            : noBorrados.Count > 0 ? $"Terminó la carga de {r.ArchivoOrigen} (revisar carpeta de entrada)"
            : $"Terminó la carga de {r.ArchivoOrigen}";
        return Crear(TipoNotificacion.Exitoso, fuente, asunto, [.. datos]);
    }

    public Notificacion Error(ConfiguracionFuente fuente, string? archivo, string mensaje, long? idCarga) =>
        Crear(TipoNotificacion.Error, fuente, $"ERROR en la carga{(archivo is null ? "" : $" de {archivo}")}",
            ("Resultado", "La carga falló. No quedaron datos a medias; en la siguiente ejecución se reintenta."),
            ("Archivo", archivo ?? "-"),
            ("Error", mensaje),
            ("Id de carga", idCarga?.ToString(CultureInfo.InvariantCulture) ?? "-"));

    private static Notificacion Crear(TipoNotificacion tipo, ConfiguracionFuente fuente, string asunto, params (string Campo, string Valor)[] datos)
    {
        var cuerpo = new StringBuilder()
            .AppendLine($"Fuente: {fuente.Nombre} (Id {fuente.IdFuente})");
        foreach (var (campo, valor) in datos)
            cuerpo.AppendLine($"{campo}: {valor}");
        cuerpo.AppendLine().Append($"Equipo: {Environment.MachineName}");

        return new Notificacion(tipo, $"[Carga {fuente.Nombre}] {asunto}", cuerpo.ToString());
    }

    private static string Fecha(DateOnly f) => f.ToString("dd/MM/yyyy", Mx);
    private static string FechaHora(DateTime f) => f.ToString("dd/MM/yyyy HH:mm:ss", Mx);
    private static string Numero(int? n) => n?.ToString("N0", Mx) ?? "-";
    private static string Duracion(TimeSpan? d) => d is { } t ? $"{(int)t.TotalMinutes}m {t.Seconds}s" : "-";
}
