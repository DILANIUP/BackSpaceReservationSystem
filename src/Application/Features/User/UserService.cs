using SpaceReservationSystem.Application.Features.User;
using SpaceReservationSystem.Domain.Enums;
using SpaceReservationSystem.Domain.Interfaces;
using SpaceReservationSystem.Domain.Primitives;

namespace SpaceReservationSystem.Application.Features.Users;

public class UserService(
    IUserRepository userRepository,
    ICareerRepository careerRepository,
    IUnitOfWork unitOfWork
)
{
    public async Task<Result<UserSummaryResponse>> AssignCareerAsync(Guid userId, Guid careerId, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(userId, ct);
        if (user is null)
            return Result.Failure<UserSummaryResponse>(Error.NotFound("User", userId.ToString()));

        var career = await careerRepository.GetByIdAsync(careerId, ct);
        if (career is null)
            return Result.Failure<UserSummaryResponse>(Error.NotFound("Career", careerId.ToString()));

        var assignResult = user.AssignCareer(careerId);
        if (assignResult.IsFailure)
            return Result.Failure<UserSummaryResponse>(assignResult.Error);

        userRepository.Update(user);
        await unitOfWork.SaveChangesAsync(ct);

        return new UserSummaryResponse(user.Id, user.Name, user.Email.Value, user.Role?.Name ?? string.Empty, user.CareerId);
    }

    // NUEVO: se usa para el selector "¿Para quién es esta reserva?".
    public async Task<List<UserSummaryResponse>> SearchAsync(RoleCode? role, Guid? careerId, CancellationToken ct)
    {
        var users = await userRepository.SearchAsync(role, careerId, ct);

        return users
            .Select(u => new UserSummaryResponse(u.Id, u.Name, u.Email.Value, u.Role?.Name ?? string.Empty, u.CareerId))
            .ToList();
    }

    // NUEVO: para que el frontend sepa su propia carrera (Coordinator/
    // Vicerrector/Bienes lo necesitan para el selector de "crear a nombre de
    // otro" y para mostrar "tu carrera: X" en pantalla) sin tener que decodificar
    // nada del JWT — el JWT no trae careerId.
    public async Task<Result<UserSummaryResponse>> GetMeAsync(Guid userId, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(userId, ct);
        if (user is null)
            return Result.Failure<UserSummaryResponse>(Error.NotFound("User", userId.ToString()));

        return new UserSummaryResponse(user.Id, user.Name, user.Email.Value, user.Role?.Name ?? string.Empty, user.CareerId);
    }
}