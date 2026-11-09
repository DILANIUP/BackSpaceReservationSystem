
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using SpaceReservationSystem.API.Abstractions.Email;

namespace SpaceReservationSystem.Infrastructure.Mail;
public class SmtpEmailService : IEmailService
{
    private readonly SmtpSettings _settings;

    public SmtpEmailService(IOptions<SmtpSettings> settings)
    {
        _settings = settings.Value;
    }

    // Envío de correos sin archivos adjuntos
    public Task SendEmailAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken ct = default)
    {
        return SendEmailAsync(
            toEmail,
            subject,
            body,
            Array.Empty<EmailAttachment>(),
            ct
        );
    }

    // Envío de correos HTML con archivos 
    public async Task SendEmailAsync(
        string toEmail,
        string subject,
        string body,
        IReadOnlyCollection<EmailAttachment> attachments,
        CancellationToken ct = default)
    {
        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            Credentials = new NetworkCredential(
                _settings.Username,
                _settings.Password
            ),
            EnableSsl = true
        };

        using var message = new MailMessage
        {
            From = new MailAddress(
                _settings.FromEmail,
                _settings.FromName
            ),
            Subject = subject,
            Body = body,
            IsBodyHtml = true
        };

        message.To.Add(toEmail);

        // Agrega los archivos adjuntos
        foreach (var file in attachments)
        {
            var stream = new MemoryStream(file.Content);

            var attachment = new Attachment(
                stream,
                file.FileName,
                file.ContentType
            );

            message.Attachments.Add(attachment);
        }

        await client.SendMailAsync(message, ct);
    }
}
