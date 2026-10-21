
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpaceReservationSystem.Application.Features.Vouchers;
using SpaceReservationSystem.Domain.Entities;
using SpaceReservationSystem.Domain.Enums;
using SpaceReservationSystem.Domain.Interfaces;

namespace reservationSystem.API.Controllers;

[ApiController]
[Route("api/voucher")]
[Authorize]
public class VoucherController(VoucherService voucherService) : ControllerBase
{
    [HttpGet("{reservationId:guid}")]
    public async Task<IActionResult> GetByReservationId(Guid reservationId, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        var userRole = Enum.Parse<RoleCode>(User.FindFirstValue(ClaimTypes.Role)!);

        var result = await voucherService.GetPdfForUserAsync(reservationId, userId, userRole, ct);

        if (result.IsSuccess)
            return File(result.Value.Bytes, "application/pdf", result.Value.FileName);

        // 403 = existe pero no tienes permiso. 404 = no existe (reserva, voucher o archivo).
        var body = new { result.Error.Code, result.Error.Description };
        return result.Error.Code == "Forbidden"
            ? StatusCode(403, body)
            : NotFound(body);
    }
}