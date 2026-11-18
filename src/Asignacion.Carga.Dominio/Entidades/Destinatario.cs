using System.Net.Mail;
using Asignacion.Carga.Dominio.Excepciones;

namespace Asignacion.Carga.Dominio.Entidades;

/// <summary>
/// Correo que recibe las notificaciones (registro de ASIG.CONFIG_NOTIFICACION).
/// </summary>
public sealed record Destinatario
{
    public string Correo { get; }
    public string? Nombre { get; }

    public Destinatario(string correo, string? nombre = null)
    {
        if (string.IsNullOrWhiteSpace(correo) || !MailAddress.TryCreate(correo.Trim(), out var direccion)
            || direccion.Address != correo.Trim())
        {
            throw new ConfiguracionInvalidaException($"El correo '{correo}' no es válido.");
        }

        Correo = direccion.Address;
        Nombre = string.IsNullOrWhiteSpace(nombre) ? null : nombre.Trim();
    }
}
