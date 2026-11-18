namespace Asignacion.Carga.Dominio.Excepciones;

/// <summary>
/// Excepción base para cualquier regla de negocio que no se cumple.
/// </summary>
public class DominioException : Exception
{
    public DominioException(string mensaje) : base(mensaje) { }

    public DominioException(string mensaje, Exception interna) : base(mensaje, interna) { }
}
