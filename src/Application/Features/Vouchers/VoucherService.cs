using QuestPDF.Infrastructure;
using FluentValidation;
using QuestPDF.Helpers;
using QuestPDF.Fluent;
using SpaceReservationSystem.Domain.Entities;
using SpaceReservationSystem.Domain.Enums;
using SpaceReservationSystem.Domain.Interfaces;
using SpaceReservationSystem.Domain.Primitives;
using SpaceReservationSystem.Application.Features.Reservations;

namespace SpaceReservationSystem.Application.Features.Vouchers;

public class VoucherService(
    IReservationRepository reservationRepository,
    IVoucherRepository voucherRepository,
    IUnitOfWork unitOfWork,
    IWebHostEnvironment env,
    IValidator<GenerateVoucherRequest> validator,
    ReservationAccessPolicy accessPolicy
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

        if (!await accessPolicy.CanViewAsync(reservation, userId, userRole, ct))
            return Result.Failure<VoucherFileResult>(
                Error.Forbidden("No tienes permiso para descargar el voucher de esta reserva."));

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
        // Código corto: "RSV-" + primeros 8 caracteres del Id en mayúsculas (igual que el ticket del front).
        var code = $"RSV-{reservation.Id.ToString("N")[..8].ToUpperInvariant()}";
        var requester = $"{reservation.User.Name} {reservation.User.LastName}".Trim();
        var generatedAt = ToEcuadorTime(DateTime.UtcNow);
        var (statusText, statusFg, statusBg) = StatusStyle(reservation.CurrentStatus);

        // Historial en orden cronológico, para la sección de trazabilidad.
        var history = reservation.ReservationHistories.OrderBy(h => h.ChangeDate).ToList();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Ink));

                page.Header().Element(c => ComposeHeader(c, code));

                page.Content().Column(col =>
                {
                    // Cuadro de estado (verde si está aprobada, rojo, ámbar o gris según el caso).
                    col.Item().PaddingTop(14).AlignLeft().Background(statusBg)
                        .PaddingVertical(5).PaddingHorizontal(14)
                        .Text(statusText.ToUpperInvariant()).Bold().FontSize(12).FontColor(statusFg);

                    Section(col, "Solicitante", s =>
                    {
                        Field(s, "Nombre", requester);
                        Field(s, "Rol", RoleToSpanish(reservation.User.Role?.Code));
                        if (reservation.User.Career is not null)
                            Field(s, "Carrera", reservation.User.Career.Name);
                    });

                    Section(col, "Datos de la reserva", s =>
                    {
                        // La fecha NO se convierte de zona: es un día de calendario (medianoche UTC).
                        // Se arma con Day/Month/Year para que nunca se corra un día.
                        Field(s, "Fecha", FormatDateSpanish(reservation.Slot.Date));
                        Field(s, "Horario", $"{reservation.Slot.StartTime:hh\\:mm} – {reservation.Slot.EndTime:hh\\:mm}");
                        Field(s, "Motivo", reservation.Reason);
                    });

                    // Solo si hay espacio: una reserva de solo recursos no muestra la sección.
                    if (reservation.Space is not null)
                    {
                        Section(col, "Espacio", s =>
                        {
                            Field(s, "Nombre", reservation.Space.Name);
                            Field(s, "Tipo", SpaceTypeToSpanish(reservation.Space.Type));
                            Field(s, "Ubicación", reservation.Space.Location);
                            Field(s, "Capacidad", $"{reservation.Space.Capacity} personas");
                        });
                    }

                    // Solo si hay recursos: se muestran en una tabla.
                    if (reservation.ReservationResources.Count > 0)
                    {
                        Section(col, "Recursos", s =>
                        {
                            s.Item().Table(table =>
                            {
                                table.ColumnsDefinition(c =>
                                {
                                    c.ConstantColumn(30);
                                    c.RelativeColumn();
                                    c.ConstantColumn(70);
                                });

                                table.Header(h =>
                                {
                                    h.Cell().Element(HeaderCell).Text("#");
                                    h.Cell().Element(HeaderCell).Text("Recurso");
                                    h.Cell().Element(HeaderCell).AlignRight().Text("Cantidad");
                                });

                                var n = 1;
                                foreach (var rr in reservation.ReservationResources)
                                {
                                    table.Cell().Element(BodyCell).Text((n++).ToString());
                                    table.Cell().Element(BodyCell).Text(rr.Resource?.Name ?? "-");
                                    table.Cell().Element(BodyCell).AlignRight().Text(rr.RequestedQuantity.ToString());
                                }
                            });
                        });
                    }

                    // Trazabilidad: quién hizo cada paso y cuándo (hora de Ecuador).
                    if (history.Count > 0)
                    {
                        Section(col, "Trazabilidad", s =>
                        {
                            s.Item().Table(table =>
                            {
                                table.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn(3);
                                    c.RelativeColumn(3);
                                    c.ConstantColumn(100);
                                });

                                table.Header(h =>
                                {
                                    h.Cell().Element(HeaderCell).Text("Etapa");
                                    h.Cell().Element(HeaderCell).Text("Responsable");
                                    h.Cell().Element(HeaderCell).Text("Fecha y hora");
                                });

                                foreach (var entry in history)
                                {
                                    var who = entry.ChangedBy is null
                                        ? "-"
                                        : $"{entry.ChangedBy.Name} {entry.ChangedBy.LastName} ({RoleToSpanish(entry.ChangedBy.Role?.Code)})";

                                    table.Cell().Element(BodyCell).Text(HistoryStageToSpanish(entry.NewStatus));
                                    table.Cell().Element(BodyCell).Text(who);
                                    table.Cell().Element(BodyCell).Text(ToEcuadorTime(entry.ChangeDate).ToString("dd/MM/yyyy HH:mm"));
                                }
                            });
                        });
                    }
                });

                page.Footer().Element(c => ComposeFooter(c, generatedAt));
            });
        }).GeneratePdf(path);
    }

    // ---- Colores del diseño (para cambiar el look basta con editar estas constantes) ----
    private const string Navy = "#1F3A5F";   // franja y títulos
    private const string Ink = "#1B2430";    // texto normal
    private const string Muted = "#5F6B7A";  // etiquetas y textos secundarios
    private const string Line = "#D0D5DD";   // líneas finas
    private const string Soft = "#F1F3F5";   // fondo de encabezados de tabla

    // Encabezado: UNIB·E a la izquierda, título y código a la derecha, franja azul debajo.
    private static void ComposeHeader(IContainer container, string code)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text("UNIB·E").FontSize(26).Bold().FontColor(Navy);
                    left.Item().Text("Universidad Iberoamericana del Ecuador").FontSize(9).FontColor(Muted);
                });

                row.RelativeItem().Column(right =>
                {
                    right.Item().AlignRight().Text("COMPROBANTE DE RESERVA").FontSize(13).Bold().FontColor(Navy);
                    right.Item().AlignRight().Text($"Código: {code}").FontSize(11).SemiBold();
                });
            });

            col.Item().PaddingTop(8).LineHorizontal(3).LineColor(Navy);
        });
    }

    // Pie: fecha de generación a la izquierda, "Página X de Y" a la derecha, leyenda debajo.
    private static void ComposeFooter(IContainer container, DateTime generatedAt)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(0.75f).LineColor(Line);

            col.Item().PaddingTop(5).Row(row =>
            {
                row.RelativeItem()
                    .Text($"Generado el {generatedAt:dd/MM/yyyy HH:mm} (hora de Ecuador)")
                    .FontSize(8).FontColor(Muted);

                row.RelativeItem().AlignRight().Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(8).FontColor(Muted));
                    t.Span("Página ");
                    t.CurrentPageNumber();
                    t.Span(" de ");
                    t.TotalPages();
                });
            });

            col.Item().PaddingTop(2)
                .Text("Documento generado automáticamente por el Sistema de Reservas UNIB·E.")
                .FontSize(8).FontColor(Muted);
        });
    }

    // Una sección: título en mayúsculas, línea fina y el contenido debajo.
    private static void Section(ColumnDescriptor col, string title, Action<ColumnDescriptor> content)
    {
        col.Item().PaddingTop(16).Column(sec =>
        {
            sec.Item().Text(title.ToUpperInvariant()).FontSize(9).Bold().FontColor(Navy);
            sec.Item().PaddingTop(2).LineHorizontal(0.75f).LineColor(Line);
            sec.Item().PaddingTop(6).Column(inner =>
            {
                inner.Spacing(4);
                content(inner);
            });
        });
    }

    // Una fila "etiqueta ..... valor".
    private static void Field(ColumnDescriptor col, string label, string value)
    {
        col.Item().Row(row =>
        {
            row.ConstantItem(95).Text(label).FontColor(Muted);
            row.RelativeItem().Text(value).SemiBold();
        });
    }

    // Estilos de celda para las tablas.
    private static IContainer HeaderCell(IContainer c) =>
        c.Background(Soft).BorderBottom(1).BorderColor(Navy)
         .PaddingVertical(5).PaddingHorizontal(6)
         .DefaultTextStyle(x => x.SemiBold().FontColor(Navy));

    private static IContainer BodyCell(IContainer c) =>
        c.BorderBottom(0.5f).BorderColor(Line).PaddingVertical(5).PaddingHorizontal(6);

    // Color del cuadro de estado: texto y fondo.
    private static (string Text, string Fg, string Bg) StatusStyle(ReservationStatus s) => s switch
    {
        ReservationStatus.Approved => ("Aprobada", "#1B5E20", "#E8F5E9"),
        ReservationStatus.Rejected or ReservationStatus.Cancelled => (StatusToSpanish(s), "#B71C1C", "#FDECEA"),
        ReservationStatus.Draft => (StatusToSpanish(s), "#455A64", "#ECEFF1"),
        _ => (StatusToSpanish(s), "#8A5A00", "#FFF4E0")
    };

    private static string RoleToSpanish(RoleCode? role) => role switch
    {
        RoleCode.Student => "Estudiante",
        RoleCode.Teacher => "Docente",
        RoleCode.Coordinator => "Coordinador",
        RoleCode.Vicerrector => "Vicerrector",
        RoleCode.Bienes => "Bienes",
        RoleCode.Admin => "Administrador",
        _ => "-"
    };

    private static string SpaceTypeToSpanish(SpaceType type) => type switch
    {
        SpaceType.Classroom => "Aula",
        SpaceType.Laboratory => "Laboratorio",
        SpaceType.Auditorium => "Auditorio",
        SpaceType.Cafeteria => "Cafetería",
        _ => "Otro"
    };

    // Describe el estado AL QUE PASÓ la reserva en cada entrada del historial.
    private static string HistoryStageToSpanish(ReservationStatus s) => s switch
    {
        ReservationStatus.PendingCoordinator => "Enviada a Coordinación",
        ReservationStatus.PendingVicerrector => "Enviada a Vicerrectorado",
        ReservationStatus.PendingAssets => "Enviada a Bienes",
        ReservationStatus.Approved => "Reserva aprobada",
        ReservationStatus.Rejected => "Rechazada",
        ReservationStatus.Cancelled => "Cancelada",
        ReservationStatus.Draft => "Devuelta a borrador",
        _ => s.ToString()
    };

    // Fecha en español escrita a mano (no depende de la cultura del servidor: en un
    // servidor sin datos de idioma, "dddd" saldría en inglés).
    private static readonly string[] DayNames =
        { "domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado" };

    private static readonly string[] MonthNames =
        { "enero", "febrero", "marzo", "abril", "mayo", "junio",
          "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre" };

    private static string FormatDateSpanish(DateTime d)
    {
        var day = DayNames[(int)d.DayOfWeek];
        return $"{char.ToUpperInvariant(day[0])}{day[1..]}, {d.Day} de {MonthNames[d.Month - 1]} de {d.Year}";
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