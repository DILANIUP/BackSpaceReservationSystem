
using FluentValidation;
using QuestPDF.Helpers;
using QuestPDF.Fluent;
using SpaceReservationSystem.Domain.Entities;
using SpaceReservationSystem.Domain.Enums;
using SpaceReservationSystem.Domain.Interfaces;
using SpaceReservationSystem.Domain.Primitives;

namespace SpaceReservationSystem.Application.Features.Vouchers;

public class VoucherService(
    IReservationRepository reservationRepository,
    IVoucherRepository voucherRepository,
    IUnitOfWork unitOfWork,
    IWebHostEnvironment env,
    IValidator<GenerateVoucherRequest> validator
)
{
    public async Task<Result<VoucherResponse>> GenerateAsync(GenerateVoucherRequest request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var f = validation.Errors[0];
            return Result.Failure<VoucherResponse>(Error.Validation(f.PropertyName, f.ErrorMessage));
        }

        var reservation = await reservationRepository.GetByIdWithDetailsAsync(request.ReservationId, ct);
        if (reservation is null)
            return Result.Failure<VoucherResponse>(Error.NotFound("Reservation", request.ReservationId.ToString()));

        if (reservation.CurrentStatus != ReservationStatus.Approved)
            return Result.Failure<VoucherResponse>(
                Error.Conflict("Voucher", "Solo se puede generar el voucher de una reserva en estado Approved.")
            );

        var folder = Path.Combine(env.ContentRootPath, "Storage", "Vouchers");
        Directory.CreateDirectory(folder);
        var fullPath = Path.Combine(folder, $"{reservation.Id}.pdf");

        BuildPdf(reservation, fullPath);

        var voucherResult = Voucher.Create(fullPath, reservation.Id);
        if (voucherResult.IsFailure)
            return Result.Failure<VoucherResponse>(voucherResult.Error);

        voucherRepository.Add(voucherResult.Value);
        await unitOfWork.SaveChangesAsync(ct);

        return new VoucherResponse(voucherResult.Value.Id, voucherResult.Value.PdfFilePath, voucherResult.Value.GenerationDate);
    }


    public async Task<Result<VoucherFileResult>> GetPdfForUserAsync(Guid reservationId, Guid userId, RoleCode userRole, CancellationToken ct)
    {
        var reservation = await reservationRepository.GetByIdAsync(reservationId, ct);
        if (reservation is null)
            return Result.Failure<VoucherFileResult>(Error.NotFound("Reservation", reservationId.ToString()));

        var isStaff = userRole is RoleCode.Admin or RoleCode.Bienes or RoleCode.Coordinator or RoleCode.Vicerrector;
        if (!isStaff && reservation.UserId != userId)
            return Result.Failure<VoucherFileResult>(Error.Conflict("Voucher", "No tienes permiso para descargar el voucher de esta reserva."));

        var voucher = await voucherRepository.GetByReservationIdAsync(reservationId, ct);
        if (voucher is null)
            return Result.Failure<VoucherFileResult>(Error.NotFound("Voucher", reservationId.ToString()));

        if (!File.Exists(voucher.PdfFilePath))
            return Result.Failure<VoucherFileResult>(
                Error.NotFound("VoucherFile", voucher.PdfFilePath));

        var bytes = await File.ReadAllBytesAsync(voucher.PdfFilePath, ct);
        return new VoucherFileResult(bytes, $"voucher-{reservationId}.pdf");
    }
    private static void BuildPdf(Reservation reservation, string path)
    {
        // Código corto y legible: "RSV-" + primeros 8 caracteres del Id en mayúsculas.
        // Es el mismo formato que usa el ticket del front. "N" quita los guiones;
        // los primeros 8 caracteres coinciden con los del formato normal del Guid.
        var code = $"RSV-{reservation.Id.ToString("N")[..8].ToUpperInvariant()}";

        // EXTRA: User.Name solo trae el nombre; el apellido está en LastName.
        var requester = $"{reservation.User.Name} {reservation.User.LastName}".Trim();

        // La hora de generación se muestra en hora de Ecuador, no en UTC.
        var generatedAt = ToEcuadorTime(DateTime.UtcNow);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Text("Comprobante de Reserva").FontSize(18).Bold().AlignCenter();

                page.Content().Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Text($"N° Reserva: {code}");
                    col.Item().Text($"Solicitante: {requester}");

                    // CAMBIO: antes salía "Espacio: N/A" en reservas de solo recursos.
                    // Ahora la línea solo aparece si hay un espacio asignado.
                    if (reservation.Space is not null)
                        col.Item().Text($"Espacio: {reservation.Space.Name}");

                    // EXTRA: en una reserva de solo recursos esto es lo único que dice
                    // qué se reservó, así que se lista con su cantidad.
                    if (reservation.ReservationResources.Count > 0)
                    {
                        col.Item().Text("Recursos:");
                        foreach (var rr in reservation.ReservationResources)
                            col.Item().Text($"  - {rr.Resource?.Name ?? "-"} x {rr.RequestedQuantity}");
                    }

                    // La fecha NO se convierte a hora local: es un día de calendario
                    // (medianoche UTC). Convertirla mostraría el día anterior.
                    col.Item().Text($"Fecha: {reservation.Slot.Date:dd/MM/yyyy}");
                    col.Item().Text($"Horario: {reservation.Slot.StartTime:hh\\:mm} - {reservation.Slot.EndTime:hh\\:mm}");
                    col.Item().Text($"Motivo: {reservation.Reason}");
                    col.Item().Text($"Estado: {StatusToSpanish(reservation.CurrentStatus)}");
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Generado el ").FontSize(9);
                    t.Span(generatedAt.ToString("dd/MM/yyyy HH:mm")).FontSize(9);
                });
            });
        }).GeneratePdf(path);
    }

    private static string StatusToSpanish(ReservationStatus status) => status switch
    {
        ReservationStatus.Draft => "Borrador",
        ReservationStatus.PendingCoordinator => "Pendiente de Coordinador",
        ReservationStatus.PendingVicerrector => "Pendiente de Vicerrector",
        ReservationStatus.PendingAssets => "Pendiente de Bienes",
        ReservationStatus.Approved => "Aprobada",
        ReservationStatus.Rejected => "Rechazada",
        ReservationStatus.Cancelled => "Cancelada",
        _ => status.ToString()
    };

    // convierte un instante UTC a hora de Ecuador 
    private static DateTime ToEcuadorTime(DateTime utc)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Guayaquil");
            return TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return utc.ToLocalTime();
        }
    }
}