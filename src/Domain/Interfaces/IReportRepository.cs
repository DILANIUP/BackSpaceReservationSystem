using SpaceReservationSystem.Domain.Models;

namespace SpaceReservationSystem.Domain.Interfaces;

public interface IReportRepository
{
    // Total de reservas del reporte (con filtros), sin límite de filas.
    Task<int> CountAsync(ReportKind kind, Guid actingUserId, ReportFilter filter, CancellationToken ct = default);

    // Filas del reporte, más recientes primero. limit = null devuelve todas (para exportar).
    Task<List<ReportSourceRow>> GetRowsAsync(ReportKind kind, Guid actingUserId, ReportFilter filter, int? limit, CancellationToken ct = default);
}