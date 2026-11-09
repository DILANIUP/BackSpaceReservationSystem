
using System.Net;
using EmailLogEntity = SpaceReservationSystem.Domain.Entities.EmailLog;
using SpaceReservationSystem.API.Abstractions.Email;
using SpaceReservationSystem.Domain.Interfaces;
using SpaceReservationSystem.Domain.Primitives;

namespace SpaceReservationSystem.Application.Features.Email;

public class EmailService
{
    private readonly IEmailTemplateRepository _templateRepository;
    private readonly IEmailLogRepository _logRepository;
    private readonly IEmailService _emailSender;
    private readonly IUnitOfWork _unitOfWork;

    public EmailService(
        IEmailTemplateRepository templateRepository,
        IEmailLogRepository logRepository,
        IEmailService emailSender,
        IUnitOfWork unitOfWork)
    {
        _templateRepository = templateRepository;
        _logRepository = logRepository;
        _emailSender = emailSender;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<EmailLogEntity>> SendByTemplateAsync(
        string templateCode,
        string toEmail,
        Guid? reservationId = null,
        Guid? alertId = null,
        CancellationToken ct = default,
        IReadOnlyDictionary<string, string>? replacements = null,
        IReadOnlyCollection<EmailAttachment>? attachments = null)
    {
        var template = await _templateRepository.GetByCodeAsync(
            templateCode, ct);

        if (template is null)
        {
            return Result.Failure<EmailLogEntity>(
                Error.NotFound("EmailTemplate", templateCode));
        }

        var logResult = EmailLogEntity.Create(
            toEmail, template.Id, reservationId, alertId);

        if (logResult.IsFailure)
            return Result.Failure<EmailLogEntity>(logResult.Error);

        var log = logResult.Value;

        try
        {
            // Reemplazar variables en el asunto y el contenido HTML.
            var subject = ReplaceMarkers(
                template.Subject,
                replacements,
                encodeHtml: false);

            var body = ReplaceMarkers(
                template.Body,
                replacements,
                encodeHtml: true);

            // Enviar correo con los archivos adjuntos
            await _emailSender.SendEmailAsync(
                toEmail,
                subject,
                body,
                attachments ?? Array.Empty<EmailAttachment>(),
                ct);

            log.MarkAsSent();
        }
        catch (Exception ex)
        {
            log.MarkAsFailed(ex.Message);
        }

        _logRepository.Add(log);

        await _unitOfWork.SaveChangesAsync(ct);

        return log;
    }

    private static string ReplaceMarkers(
        string content,
        IReadOnlyDictionary<string, string>? replacements,
        bool encodeHtml)
    {
        if (replacements is null || replacements.Count == 0)
            return content;

        foreach (var replacement in replacements)
        {
            var value = encodeHtml
                ? WebUtility.HtmlEncode(replacement.Value)
                : replacement.Value;

            content = content.Replace(
                "@" + replacement.Key,
                value,
                StringComparison.OrdinalIgnoreCase);
        }

        return content;
    }
}
