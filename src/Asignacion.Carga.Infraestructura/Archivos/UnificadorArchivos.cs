using System.Globalization;
using System.Security.Cryptography;
using Asignacion.Carga.Aplicacion.Interfaces;
using Asignacion.Carga.Dominio.Entidades;
using Asignacion.Carga.Dominio.Excepciones;
using Asignacion.Carga.Dominio.Servicios;
using Asignacion.Carga.Infraestructura.Excel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Asignacion.Carga.Infraestructura.Archivos;

/// <summary>
/// Une en un solo .xlsx todo lo que el usuario dejó en la carpeta de entrada, sin importar cómo:
/// zip (con o sin contraseña, zip dentro de zip), carpetas con subcarpetas o Excel sueltos.
/// - Cada Excel se lee de la hoja configurada; si la columna A trae los campos separados
///   (p. ej. por "|"), se separan; si ya viene en columnas, se toman tal cual.
/// - Todos deben traer los mismos encabezados.
/// - Un Excel idéntico que aparece más de una vez (en el zip y en la carpeta) se toma una sola vez.
/// - Se aplican las correcciones de columnas (p. ej. TIPO_GESTION_>GESTORES).
/// Todo se hace en una carpeta temporal: la entrada no se toca hasta <see cref="VaciarEntradaAsync"/>.
/// </summary>
internal sealed class UnificadorArchivos : IUnificadorArchivos
{
    private static readonly string[] Ignorados = ["thumbs.db", "desktop.ini"];

    private readonly OpcionesUnificacion _opciones;
    private readonly ILogger<UnificadorArchivos> _log;

    public UnificadorArchivos(IOptions<OpcionesUnificacion> opciones, ILogger<UnificadorArchivos> log)
    {
        _opciones = opciones.Value;
        _log = log;
    }

    public Task<ArchivoUnificado?> UnificarAsync(ConfiguracionFuente fuente, DateOnly fecha, CancellationToken ct) =>
        Task.Run(() => Unificar(fuente, fecha, ct), ct);

    public Task PublicarAsync(ArchivoUnificado unificado, CancellationToken ct) => Task.Run(() =>
    {
        Directory.CreateDirectory(unificado.Destino.Carpeta); // carpeta del mes si no existe
        var final = unificado.Destino.RutaCompleta;
        var temporal = final + ".tmp";
        try
        {
            File.Copy(unificado.Lectura.RutaCompleta, temporal, overwrite: true);
            File.Move(temporal, final, overwrite: true);
        }
        catch (IOException ex)
        {
            TryBorrar(temporal);
            throw new IOException($"No se pudo guardar '{final}'. ¿Alguien lo tiene abierto en Excel? {ex.Message}", ex);
        }
        _log.LogInformation("Unificado guardado en {Ruta}.", final);
    }, ct);

    public Task<IReadOnlyList<string>> VaciarEntradaAsync(ArchivoUnificado unificado, CancellationToken ct) =>
        Task.Run<IReadOnlyList<string>>(() =>
        {
            var noBorrados = new List<string>();
            foreach (var entrada in unificado.Entradas) // solo lo que había al empezar; lo que llegó después se queda
            {
                try
                {
                    if (Directory.Exists(entrada)) Directory.Delete(entrada, recursive: true);
                    else if (File.Exists(entrada)) File.Delete(entrada);
                    _log.LogInformation("Borrado de la entrada: {Entrada}", entrada);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _log.LogWarning("No se pudo borrar {Entrada}: {Mensaje}", entrada, ex.Message);
                    noBorrados.Add(entrada);
                }
            }
            return noBorrados;
        }, ct);

    public Task DescartarTemporalesAsync(ArchivoUnificado unificado)
    {
        TryBorrarCarpeta(unificado.CarpetaTemporal);
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------

    private ArchivoUnificado? Unificar(ConfiguracionFuente fuente, DateOnly fecha, CancellationToken ct)
    {
        if (!Directory.Exists(fuente.RutaOrigen))
            throw new DirectoryNotFoundException(
                $"No se puede acceder a la carpeta de entrada '{fuente.RutaOrigen}'. Verifique que exista y los permisos del usuario.");

        var entradas = Directory.EnumerateFileSystemEntries(fuente.RutaOrigen)
            .Where(e => !ArchivoOrigen.EsTemporalDeOffice(e) && !Ignorados.Contains(Path.GetFileName(e).ToLowerInvariant()))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (entradas.Count == 0) return null;

        var nombreSalida = fuente.ResolverNombreSalida(fecha);
        var temporal = CrearCarpetaTemporal(fuente);
        try
        {
            var excels = ReunirExcel(entradas, Path.Combine(temporal, "entrada"), ct);
            var (unicos, repetidos) = SinRepetidos(excels, ct);
            if (unicos.Count == 0)
                throw new DominioException($"En '{fuente.RutaOrigen}' hay {entradas.Count} elemento(s), pero ningún Excel (.xlsx) para unificar.");
            if (repetidos > 0)
                _log.LogWarning("{Repetidos} Excel venían repetidos (mismo contenido); se toman una sola vez.", repetidos);

            var encabezados = EncabezadosComunes(fuente, unicos, fecha, ct);
            var corrector = new CorrectorColumnas(encabezados, fuente.CorreccionesColumnas);
            var movidos = 0;

            var rutaUnificado = Path.Combine(temporal, nombreSalida);
            var filas = EscritorExcelOpenXml.Escribir(rutaUnificado, fuente.HojaSalida, encabezados,
                FilasDe(fuente, unicos, encabezados, corrector, fecha, m => movidos += m, ct), ct);

            if (corrector.TieneCorrecciones)
                _log.LogInformation("Correcciones de columnas aplicadas en {Movidos} fila(s).", movidos);

            var ahora = DateTime.Now;
            return new ArchivoUnificado(
                Lectura: new ArchivoOrigen(nombreSalida, temporal, fecha, ahora),
                Destino: new ArchivoOrigen(nombreSalida, fuente.ResolverRutaSalida(fecha), fecha, ahora),
                Hoja: fuente.HojaSalida,
                Entradas: entradas,
                ArchivosUnificados: unicos.Count,
                ArchivosRepetidos: repetidos,
                Filas: filas,
                CarpetaTemporal: temporal);
        }
        catch
        {
            TryBorrarCarpeta(temporal);
            throw;
        }
    }

    /// <summary>Todos los .xlsx de la entrada: sueltos, dentro de carpetas o dentro de zip (se extraen al temporal).</summary>
    private List<string> ReunirExcel(IReadOnlyList<string> entradas, string extraidos, CancellationToken ct)
    {
        var contrasena = Environment.GetEnvironmentVariable(_opciones.VariableContrasena);
        if (string.IsNullOrEmpty(contrasena)) contrasena = string.IsNullOrEmpty(_opciones.Contrasena) ? null : _opciones.Contrasena;
        var excels = new List<string>();
        var n = 0;

        void ExtraerZip(string zip)
        {
            _log.LogInformation("Extrayendo {Zip}", zip);
            ExtractorZip.Extraer(zip, Path.Combine(extraidos, $"{n++:00}_{Path.GetFileNameWithoutExtension(zip)}"), contrasena, ct);
        }

        foreach (var entrada in entradas)
        {
            if (Directory.Exists(entrada))
            {
                foreach (var zip in Directory.EnumerateFiles(entrada, "*.zip", SearchOption.AllDirectories)) ExtraerZip(zip);
                excels.AddRange(Directory.EnumerateFiles(entrada, "*.xlsx", SearchOption.AllDirectories));
            }
            else if (entrada.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) ExtraerZip(entrada);
            else if (entrada.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) excels.Add(entrada);
            else _log.LogWarning("Se ignora (no es zip, carpeta ni .xlsx): {Entrada}", entrada);
        }

        if (Directory.Exists(extraidos))
            excels.AddRange(Directory.EnumerateFiles(extraidos, "*.xlsx", SearchOption.AllDirectories));

        return excels.Where(e => !ArchivoOrigen.EsTemporalDeOffice(e)).ToList();
    }

    private static (List<string> Unicos, int Repetidos) SinRepetidos(IEnumerable<string> excels, CancellationToken ct)
    {
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        var unicos = new List<string>();
        var repetidos = 0;
        foreach (var excel in excels)
        {
            ct.ThrowIfCancellationRequested();
            using var flujo = File.OpenRead(excel);
            if (vistos.Add(Convert.ToHexString(SHA256.HashData(flujo)))) unicos.Add(excel);
            else repetidos++;
        }
        return (unicos, repetidos);
    }

    private static string[] EncabezadosComunes(ConfiguracionFuente fuente, IReadOnlyList<string> excels, DateOnly fecha, CancellationToken ct)
    {
        string[]? comunes = null;
        string? primero = null;
        foreach (var excel in excels)
        {
            using var libro = Abrir(excel, fuente, fecha);
            var encabezados = libro.Filas(ct)
                .Where(f => f.Numero == fuente.FilaEncabezado)
                .Select(f => Campos(f.Celdas, fuente.Separador!, null).Select(c => c?.Trim() ?? "").ToArray())
                .FirstOrDefault()
                ?? throw new DominioException($"'{Path.GetFileName(excel)}' no tiene encabezados en la fila {fuente.FilaEncabezado} de la hoja '{fuente.Hoja}'.");

            if (comunes is null) { comunes = encabezados; primero = excel; continue; }
            if (!comunes.SequenceEqual(encabezados, StringComparer.Ordinal))
                throw new DominioException(
                    $"'{Path.GetFileName(excel)}' no trae los mismos encabezados que '{Path.GetFileName(primero)}' ({encabezados.Length} vs {comunes.Length} columnas). No se unificó nada.");
        }
        return comunes!;
    }

    private static IEnumerable<string?[]> FilasDe(
        ConfiguracionFuente fuente, IReadOnlyList<string> excels, string[] encabezados, CorrectorColumnas corrector,
        DateOnly fecha, Action<int> movidos, CancellationToken ct)
    {
        foreach (var excel in excels)
        {
            using var libro = Abrir(excel, fuente, fecha);
            foreach (var (numero, celdas) in libro.Filas(ct))
            {
                if (numero <= fuente.FilaEncabezado) continue;
                var campos = Campos(celdas, fuente.Separador!, encabezados.Length);
                if (campos.All(string.IsNullOrWhiteSpace)) continue;
                if (campos.Length != encabezados.Length)
                    throw new DominioException(
                        $"'{Path.GetFileName(excel)}', fila {numero}: trae {campos.Length} campos y se esperaban {encabezados.Length}. No se unificó nada.");

                movidos(corrector.Aplicar(campos));
                yield return campos;
            }
        }
    }

    /// <summary>
    /// Campos de una fila. Si solo trae la columna A con el separador, se separa; si viene en columnas, se toman tal cual.
    /// </summary>
    private static string?[] Campos(IReadOnlyList<(int Columna, object? Valor)> celdas, string separador, int? columnas)
    {
        var conDato = celdas.Where(c => c.Valor is not null).ToList();
        if (conDato.Count == 1 && conDato[0].Columna == 0 && conDato[0].Valor is string texto && texto.Contains(separador, StringComparison.Ordinal))
            return texto.Split(separador).Select(v => v.Length == 0 ? null : v).ToArray();

        var ultima = conDato.Count == 0 ? 0 : conDato.Max(c => c.Columna) + 1;
        var campos = new string?[Math.Max(columnas ?? 0, ultima)];
        foreach (var (columna, valor) in conDato)
            campos[columna] = valor switch
            {
                DateTime f => f.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                double d => ((decimal)d).ToString(CultureInfo.InvariantCulture),
                _ => Convert.ToString(valor, CultureInfo.InvariantCulture)
            };
        return campos;
    }

    private static LectorExcelOpenXml.LibroAbierto Abrir(string excel, ConfiguracionFuente fuente, DateOnly fecha) =>
        LectorExcelOpenXml.LibroAbierto.Abrir(
            new ArchivoOrigen(Path.GetFileName(excel), Path.GetDirectoryName(excel)!, fecha, File.GetLastWriteTime(excel)), fuente.Hoja);

    private string CrearCarpetaTemporal(ConfiguracionFuente fuente)
    {
        var raiz = string.IsNullOrWhiteSpace(_opciones.CarpetaTemporal) ? Path.GetTempPath() : _opciones.CarpetaTemporal;
        var carpeta = Path.Combine(raiz, "Asignacion", $"{fuente.Nombre}_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..8]}");
        Directory.CreateDirectory(carpeta);
        return carpeta;
    }

    private static void TryBorrar(string archivo)
    {
        try { if (File.Exists(archivo)) File.Delete(archivo); } catch { /* temporal: no es crítico */ }
    }

    private static void TryBorrarCarpeta(string carpeta)
    {
        try { if (Directory.Exists(carpeta)) Directory.Delete(carpeta, recursive: true); } catch { /* temporal: no es crítico */ }
    }
}
