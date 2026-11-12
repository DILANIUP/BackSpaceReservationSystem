namespace SpaceReservationSystem.Application.Features.Reports;

public sealed record ReportTypeResponse(
    string Code,
    string Name
);

public sealed record ReportRowResponse(
    string Code,
    string Requester,
    string Date,
    string StartTime,
    string EndTime,
    string? Space,
    string Resources,
    string Reason,
    string Status,
    string StatusText,
    DateTime RequestDate
);

public sealed record ReportPreviewResponse(int Total, List<ReportRowResponse> Rows);