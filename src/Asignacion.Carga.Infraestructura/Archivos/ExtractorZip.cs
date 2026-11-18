using Asignacion.Carga.Dominio.Excepciones;
using ICSharpCode.SharpZipLib.Zip;

namespace Asignacion.Carga.Infraestructura.Archivos;

/// <summary>
/// Extrae zip con o sin contraseña (ZipCrypto o AES) usando SharpZipLib,
/// incluyendo zip que vienen dentro de otros zip.
/// </summary>
internal static class ExtractorZip
{
    private const int ProfundidadMaxima = 5;

    /// <summary>Extrae <paramref name="zip"/> en <paramref name="destino"/> y, a su vez, los zip que traiga dentro.</summary>
    public static void Extraer(string zip, string destino, string? contrasena, CancellationToken ct, int profundidad = 0)
    {
        if (profundidad > ProfundidadMaxima)
            throw new DominioException($"'{Path.GetFileName(zip)}' tiene demasiados zip anidados (más de {ProfundidadMaxima}).");

        Directory.CreateDirectory(destino);
        var raiz = Path.GetFullPath(destino) + Path.DirectorySeparatorChar;

        using (var archivo = new ZipFile(File.OpenRead(zip)))
        {
            foreach (ZipEntry entrada in archivo)
            {
                ct.ThrowIfCancellationRequested();
                if (!entrada.IsFile) continue;

                // Evita "zip slip": que una entrada con ..\ escriba fuera de la carpeta temporal
                var ruta = Path.GetFullPath(Path.Combine(destino, entrada.Name.Replace('/', Path.DirectorySeparatorChar)));
                if (!ruta.StartsWith(raiz, StringComparison.OrdinalIgnoreCase))
                    throw new DominioException($"'{Path.GetFileName(zip)}' trae una ruta no permitida: {entrada.Name}");

                if (entrada.IsCrypted)
                {
                    if (string.IsNullOrEmpty(contrasena))
                        throw new DominioException(
                            $"'{Path.GetFileName(zip)}' pide contraseña y no está definida la variable de entorno {OpcionesUnificacion.VariableContrasenaPorOmision}.");
                    archivo.Password = contrasena;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
                try
                {
                    using var origen = archivo.GetInputStream(entrada);
                    using var salida = File.Create(ruta);
                    origen.CopyTo(salida);
                }
                catch (ZipException ex) when (entrada.IsCrypted)
                {
                    throw new DominioException($"No se pudo abrir '{entrada.Name}' de '{Path.GetFileName(zip)}': contraseña incorrecta o zip dañado. {ex.Message}");
                }
            }
        }

        // Zip dentro del zip: se extraen en una carpeta con su nombre y se borra el zip interno
        foreach (var interno in Directory.EnumerateFiles(destino, "*.zip", SearchOption.AllDirectories).ToList())
        {
            var carpeta = Path.Combine(Path.GetDirectoryName(interno)!, Path.GetFileNameWithoutExtension(interno) + "_zip");
            Extraer(interno, carpeta, contrasena, ct, profundidad + 1);
            File.Delete(interno);
        }
    }
}
