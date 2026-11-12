using SpaceReservationSystem.Domain.Enums;
using SpaceReservationSystem.Domain.Interfaces;
using SpaceReservationSystem.Domain.Models;
using SpaceReservationSystem.Domain.Primitives;

namespace SpaceReservationSystem.Application.Features.Reports;

public class ReportService(IReportRepository reportRepository)
{
    private const int PreviewLimit = 50;

    // Catálogo único: código, nombre, tipo de consulta y roles permitidos.
    private sealed record ReportDefinition(string Code, string Name, ReportKind Kind, RoleCode[]? Roles);

    private static readonly ReportDefinition[] Catalog =
    {
        new("my-reservations",     "Mis reservas",                              ReportKind.MyReservations,     null),
        new("coordinator-handled", "Reservas que elevé o rechacé",              ReportKind.CoordinatorHandled, new[] { RoleCode.Coordinator }),
        new("coordinator-assisted","Reservas creadas a nombre de otros",        ReportKind.CoordinatorAssisted,new[] { RoleCode.Coordinator }),
        new("vicerrector-handled", "Reservas que aprobé o rechacé",             ReportKind.VicerrectorHandled, new[] { RoleCode.Vicerrector }),
        new("assets-assigned",     "Reservas que asigné",                       ReportKind.AssetsAssigned,     new[] { RoleCode.Bienes }),
        new("admin-all",           "Todas las reservas",                        ReportKind.AdminAll,           new[] { RoleCode.Admin }),
    };

    // GET /api/reports/types: solo los tipos que le corresponden a ese rol.
    public IEnumerable<ReportTypeResponse> GetTypes(RoleCode role)
        => Catalog
            .Where(d => d.Roles is null || d.Roles.Contains(role))
            .Select(d => new ReportTypeResponse(d.Code, d.Name));

    // Vista previa
    public async Task<Result<ReportPreviewResponse>> GetPreviewAsync(
        string typeCode, ReportFilter filter, Guid userId, RoleCode role, CancellationToken ct)
    {
        var kind = Resolve(typeCode, filter, role);
        if (kind.IsFailure)
            return Result.Failure<ReportPreviewResponse>(kind.Error);

        var total = await reportRepository.CountAsync(kind.Value, userId, filter, ct);
        var rows = await reportRepository.GetRowsAsync(kind.Value, userId, filter, PreviewLimit, ct);

        return new ReportPreviewResponse(total, rows.Select(ToRow).ToList());
    }

    public async Task<Result<List<ReportRowResponse>>> GetAllAsync(
        string typeCode, ReportFilter filter, Guid userId, RoleCode role, CancellationToken ct)
    {
        var kind = Resolve(typeCode, filter, role);
        if (kind.IsFailure)
            return Result.Failure<List<ReportRowResponse>>(kind.Error);

        var rows = await reportRepository.GetRowsAsync(kind.Value, userId, filter, limit: null, ct);

        return rows.Select(ToRow).ToList();
    }


    private static Result<ReportKind> Resolve(string typeCode, ReportFilter filter, RoleCode role)
    {
        var definition = Catalog.FirstOrDefault(d => d.Code == typeCode);
        if (definition is null)
            return Result.Failure<ReportKind>(Error.NotFound("ReportType", typeCode));

        if (definition.Roles is not null && !definition.Roles.Contains(role))
            return Result.Failure<ReportKind>(Error.Forbidden("No tienes permiso para este tipo de reporte."));

        if (filter.From is { } from && filter.To is { } to && from > to)
            return Result.Failure<ReportKind>(
                Error.Validation("From", "La fecha inicial no puede ser posterior a la final."));

        return definition.Kind;
    }

    private static ReportRowResponse ToRow(ReportSourceRow r) => new(
        $"RSV-{r.Id.ToString("N")[..8].ToUpperInvariant()}",
        string.Join(' ', new[] { r.RequesterName, r.RequesterLastName }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim())),
        r.Date.ToString("yyyy-MM-dd"), // sin convertir de zona: es un día de calendario
        r.StartTime.ToString(@"hh\:mm"),
        r.EndTime.ToString(@"hh\:mm"),
        r.SpaceName,
        string.Join(", ", r.Resources.Select(x => $"{x.Name} x{x.Quantity}")),
        r.Reason,
        r.Status.ToString(),
        StatusToSpanish(r.Status),
        r.RequestDate);

    private static string StatusToSpanish(ReservationStatus s) => s switch
    {
        ReservationStatus.Draft => "Borrador",
        ReservationStatus.PendingCoordinator => "Pendiente de Coordinador",
        ReservationStatus.PendingVicerrector => "Pendiente de Vicerrector",
        ReservationStatus.PendingAssets => "Pendiente de Bienes",
        ReservationStatus.Approved => "Aprobada",
        ReservationStatus.Rejected => "Rechazada",
        ReservationStatus.Cancelled => "Cancelada",
        _ => s.ToString()
    };
}