namespace SpaceReservationSystem.API.Abstractions.Email;

public interface IEmailService
{
    //enio de correos
    Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken ct = default);

    //envio de correos con arvchivo PDF
    Task SendEmailAsync(string toEmail, string subject, string body, IReadOnlyCollection<EmailAttachment> attachments, CancellationToken ct = default);
}