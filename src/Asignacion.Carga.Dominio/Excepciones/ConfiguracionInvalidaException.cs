namespace Asignacion.Carga.Dominio.Excepciones;

/// <summary>
/// La configuración de una fuente (CONFIG_FUENTE) o de un destinatario no es válida.
/// </summary>
public sealed class ConfiguracionInvalidaException : DominioException
{
    public ConfiguracionInvalidaException(string mensaje) : base(mensaje) { }
}
