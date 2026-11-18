using Asignacion.Carga.Aplicacion.Interfaces;
using Asignacion.Carga.Aplicacion.Notificaciones;
using Asignacion.Carga.Dominio.Entidades;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Asignacion.Carga.Infraestructura.Notificaciones;

/// <summary>
/// Envía las notificaciones por SMTP con MailKit (Gmail, Office 365 o relay interno).
/// </summary>
internal sealed class NotificadorCorreoMailKit : INotificador
{
    private readonly OpcionesCorreo _opciones;
    private readonly ILogger<NotificadorCorreoMailKit> _log;

    public NotificadorCorreoMailKit(IOptions<OpcionesCorreo> opciones, ILogger<NotificadorCorreoMailKit> log)
    {
        _opciones = opciones.Value;
        _log = log;
    }

    public async Task EnviarAsync(Notificacion notificacion, IReadOnlyList<Destinatario> destinatarios, CancellationToken ct)
    {
        if (!_opciones.Habilitado)
        {
            _log.LogInformation("Correo deshabilitado. Se habría enviado: {Asunto}", notificacion.Asunto);
            return;
        }
        if (destinatarios.Count == 0) return;

        using var mensaje = CrearMensaje(notificacion, destinatarios);
        using var smtp = new SmtpClient { Timeout = _opciones.TiempoEsperaSegundos * 1000 };

        await smtp.ConnectAsync(_opciones.Servidor, _opciones.Puerto, Seguridad(), ct);
        if (!string.IsNullOrWhiteSpace(_opciones.Usuario))
            await smtp.AuthenticateAsync(_opciones.Usuario, Contrasena(), ct);
        await smtp.SendAsync(new SendRequest(mensaje), ct); // API de MailKit 5
        await smtp.DisconnectAsync(true, ct);

        _log.LogInformation("Correo enviado a {Total} destinatario(s): {Asunto}", destinatarios.Count, notificacion.Asunto);
    }

    private MimeMessage CrearMensaje(Notificacion notificacion, IReadOnlyList<Destinatario> destinatarios)
    {
        var remitente = string.IsNullOrWhiteSpace(_opciones.Remitente) ? _opciones.Usuario : _opciones.Remitente;
        if (string.IsNullOrWhiteSpace(remitente))
            throw new InvalidOperationException($"Configure '{OpcionesCorreo.Seccion}:Remitente' o '{OpcionesCorreo.Seccion}:Usuario' en el appsettings.");

        var mensaje = new MimeMessage { Subject = notificacion.Asunto };
        mensaje.From.Add(new MailboxAddress(_opciones.NombreRemitente, remitente));
        foreach (var d in destinatarios)
            mensaje.To.Add(new MailboxAddress(d.Nombre ?? d.Correo, d.Correo));
        mensaje.Body = new TextPart("plain") { Text = notificacion.Cuerpo };
        return mensaje;
    }

    private string Contrasena()
    {
        var valor = Environment.GetEnvironmentVariable(_opciones.VariableContrasena);
        if (string.IsNullOrWhiteSpace(valor)) valor = _opciones.Contrasena;
        if (string.IsNullOrWhiteSpace(valor))
            throw new InvalidOperationException(
                $"No hay contraseña para '{_opciones.Usuario}': defina la variable de entorno '{_opciones.VariableContrasena}' o '{OpcionesCorreo.Seccion}:Contrasena' en el appsettings.");
        return valor.Replace(" ", ""); // Google muestra la contraseña de aplicación con espacios
    }

    private SecureSocketOptions Seguridad() =>
        Enum.TryParse<SecureSocketOptions>(_opciones.Seguridad, ignoreCase: true, out var s)
            ? s
            : throw new InvalidOperationException($"'{OpcionesCorreo.Seccion}:Seguridad' no válida: '{_opciones.Seguridad}'. Use StartTls, SslOnConnect, None o Auto.");
}
