using SpaceReservationSystem.Domain.Models;

namespace SpaceReservationSystem.Application.Features.Reports;

public interface IReportExcelExporter
{
    byte[] Export(string reportName, ReportFilter filter,
                  IReadOnlyList<ReportRowResponse> rows, DateTime generatedAtUtc);
}