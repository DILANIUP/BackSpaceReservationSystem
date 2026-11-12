using System.Globalization;
using ClosedXML.Excel;
using SpaceReservationSystem.Application.Features.Reports;
using SpaceReservationSystem.Domain.Enums;
using SpaceReservationSystem.Domain.Models;

namespace SpaceReservationSystem.Infrastructure.Reports;

public class ClosedXmlReportExporter : IReportExcelExporter
{
    private const int HeaderRow = 4;
    private const int ColCount = 10;
    // Ecuador es UTC-5 todo el año: se convierte con offset fijo.
    private const int EcuadorOffsetHours = -5;

    private static readonly string[] Headers =
    {
        "Código", "Solicitante", "Fecha", "Inicio", "Fin", "Espacio",
        "Recursos", "Motivo", "Estado", "Fecha de solicitud"
    };

    public byte[] Export(string reportName, ReportFilter filter,
                         IReadOnlyList<ReportRowResponse> rows, DateTime generatedAtUtc)
    {
        using var wb = new XLWorkbook();
        BuildReportSheet(wb.AddWorksheet("Reporte"), reportName, filter, rows, generatedAtUtc);
        BuildSummarySheet(wb.AddWorksheet("Resumen"), reportName, rows);

        // En memoria: nada se escribe en disco.
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void BuildReportSheet(IXLWorksheet ws, string reportName, ReportFilter filter,
                                         IReadOnlyList<ReportRowResponse> rows, DateTime generatedAtUtc)
    {
        // Fila 1: título. Fila 2: filtros + fecha de generación.
        ws.Cell(1, 1).Value = reportName;
        ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
        ws.Cell(2, 1).Value = DescribeFilters(filter, generatedAtUtc);
        ws.Cell(2, 1).Style.Font.SetItalic().Font.SetFontColor(XLColor.Gray);

        // Fila 4: encabezados.
        for (var c = 0; c < ColCount; c++)
            ws.Cell(HeaderRow, c + 1).Value = Headers[c];

        var header = ws.Range(HeaderRow, 1, HeaderRow, ColCount);
        header.Style.Font.SetBold().Font.SetFontColor(XLColor.White);
        header.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#0B4F7A"));
        header.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);

        // Filas de datos desde la 5.
        var row = HeaderRow;
        foreach (var r in rows)
        {
            row++;
            ws.Cell(row, 1).Value = r.Code;
            ws.Cell(row, 2).Value = r.Requester;

            // Valores REALES de Excel (no texto) para poder ordenar y filtrar por fecha/hora.
            // Si el parseo falla (no debería), se deja el texto original.
            if (DateTime.TryParseExact(r.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                ws.Cell(row, 3).Value = d; else ws.Cell(row, 3).Value = r.Date;
            if (TimeSpan.TryParseExact(r.StartTime, @"hh\:mm", CultureInfo.InvariantCulture, out var s))
                ws.Cell(row, 4).Value = s; else ws.Cell(row, 4).Value = r.StartTime;
            if (TimeSpan.TryParseExact(r.EndTime, @"hh\:mm", CultureInfo.InvariantCulture, out var e))
                ws.Cell(row, 5).Value = e; else ws.Cell(row, 5).Value = r.EndTime;

            ws.Cell(row, 6).Value = r.Space ?? "—";
            ws.Cell(row, 7).Value = string.IsNullOrWhiteSpace(r.Resources) ? "—" : r.Resources;
            ws.Cell(row, 8).Value = r.Reason;
            ws.Cell(row, 9).Value = r.StatusText;

            // RequestDate llega en UTC: se muestra en hora de Ecuador.
            ws.Cell(row, 10).Value = DateTime.SpecifyKind(r.RequestDate, DateTimeKind.Utc)
                                             .AddHours(EcuadorOffsetHours);
        }

        var lastRow = Math.Max(row, HeaderRow);

        if (rows.Count > 0)
        {
            ws.Range(HeaderRow + 1, 3, lastRow, 3).Style.NumberFormat.Format = "dd/MM/yyyy";
            ws.Range(HeaderRow + 1, 4, lastRow, 5).Style.NumberFormat.Format = "hh:mm";
            ws.Range(HeaderRow + 1, 10, lastRow, 10).Style.NumberFormat.Format = "dd/MM/yyyy HH:mm";
            ws.Range(HeaderRow + 1, 1, lastRow, ColCount).Style.Alignment.SetVertical(XLAlignmentVerticalValues.Top);
        }

        // Autofiltro sobre encabezados + datos, y congela hasta la fila de encabezados.
        ws.Range(HeaderRow, 1, lastRow, ColCount).SetAutoFilter();
        ws.SheetView.FreezeRows(HeaderRow);

        // Anchos: se ajusta desde el encabezado (el título largo de la fila 1 no cuenta),
        // con mínimo 8 y tope 40. Motivo fijo en 50 y con ajuste de texto (igual Recursos).
        ws.Columns(1, ColCount).AdjustToContents(HeaderRow, lastRow, 8, 40);
        ws.Column(8).Width = 50;
        ws.Range(HeaderRow + 1, 7, Math.Max(lastRow, HeaderRow + 1), 8).Style.Alignment.SetWrapText();

        // Sin filas: el archivo se genera igual, con un aviso (después del autoajuste
        // para que el mensaje no ensanche la columna A).
        if (rows.Count == 0)
        {
            ws.Cell(HeaderRow + 1, 1).Value = "Sin resultados para los filtros aplicados";
            ws.Cell(HeaderRow + 1, 1).Style.Font.SetItalic().Font.SetFontColor(XLColor.Gray);
        }
    }

    private static void BuildSummarySheet(IXLWorksheet ws, string reportName, IReadOnlyList<ReportRowResponse> rows)
    {
        ws.Cell(1, 1).Value = $"Resumen — {reportName}";
        ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);

        ws.Cell(3, 1).Value = "Total de filas";
        ws.Cell(3, 1).Style.Font.SetBold();
        ws.Cell(3, 2).Value = rows.Count;

        ws.Cell(5, 1).Value = "Estado";
        ws.Cell(5, 2).Value = "Cantidad";
        var header = ws.Range(5, 1, 5, 2);
        header.Style.Font.SetBold().Font.SetFontColor(XLColor.White);
        header.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#0B4F7A"));

        // Conteo por estado (texto en español), de mayor a menor.
        var row = 5;
        foreach (var g in rows.GroupBy(r => r.StatusText).OrderByDescending(g => g.Count()))
        {
            row++;
            ws.Cell(row, 1).Value = g.Key;
            ws.Cell(row, 2).Value = g.Count();
        }

        ws.Columns(1, 2).AdjustToContents(3, Math.Max(row, 5), 12, 40);
        ws.Column(1).Width = Math.Max(ws.Column(1).Width, 24); // que "Total de filas" y estados largos quepan
    }

    // Los filtros de espacio/recurso/solicitante/carrera llegan como Guid: no hay nombre
    // disponible aquí, así que solo se indica que se aplicaron.
    private static string DescribeFilters(ReportFilter f, DateTime generatedAtUtc)
    {
        var parts = new List<string>();
        if (f.From is { } from) parts.Add($"Desde: {from:dd/MM/yyyy}");
        if (f.To is { } to) parts.Add($"Hasta: {to:dd/MM/yyyy}");
        if (f.Status is { } st) parts.Add($"Estado: {StatusToSpanish(st)}");
        if (f.SpaceId is not null) parts.Add("Espacio: filtrado");
        if (f.ResourceId is not null) parts.Add("Recurso: filtrado");
        if (f.UserId is not null) parts.Add("Solicitante: filtrado");
        if (f.CareerId is not null) parts.Add("Carrera: filtrada");

        var filters = parts.Count > 0 ? string.Join(" | ", parts) : "Sin filtros";
        var generated = generatedAtUtc.AddHours(EcuadorOffsetHours);
        return $"Filtros: {filters}   —   Generado: {generated:dd/MM/yyyy HH:mm} (hora de Ecuador)";
    }

    // Mismo texto que ReportService (allá es privado y no se toca el trabajo de IA-A).
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