using SpaceReservationSystem.Application.Features.User;
using SpaceReservationSystem.Domain.Enums;
using SpaceReservationSystem.Domain.Errors;
using SpaceReservationSystem.Domain.Interfaces;
using SpaceReservationSystem.Domain.Primitives;

namespace SpaceReservationSystem.Application.Features.Users;

public class UserService(
    IUserRepository userRepository,
    ICareerRepository careerRepository,
    IRoleRepository roleRepository,
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

    public async Task<List<UserListItemResponse>> SearchForAdminAsync(
        RoleCode? role, Guid? careerId, string? search, CancellationToken ct)
    {
        var users = await userRepository.SearchForAdminAsync(role, careerId, search, ct);

        return users
            .Select(u => new UserListItemResponse(
                u.Id,
                u.Name,
                u.Email.Value,
                u.Phone,
                u.Role?.Name ?? string.Empty,
                u.CareerId,
                u.Career?.Name,
                u.IsActive))
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

    public async Task<Result> SetStatusAsync(Guid userId, bool isActive, Guid actingUserId, CancellationToken ct)
    {
        if (userId == actingUserId && !isActive)
            return Result.Failure(Error.Validation("User", "No puedes desactivar tu propia cuenta."));

        var user = await userRepository.GetByIdAsync(userId, ct);
        if (user is null)
            return Result.Failure(Error.NotFound("User", userId.ToString()));

        if (isActive)
            user.Activate();
        else
            user.Deactivate();

        userRepository.Update(user);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    // NUEVO: editar un usuario (solo Admin). Aplica las mismas reglas que al crear.
    public async Task<Result<UserSummaryResponse>> UpdateAsync(Guid userId, EditUserRequest request, Guid actingUserId, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(userId, ct);
        if (user is null)
            return Result.Failure<UserSummaryResponse>(Error.NotFound("User", userId.ToString()));

        // Un Admin no puede quitarse a sí mismo el rol de Admin: se quedaría
        // sin acceso a esta pantalla y nadie más podría arreglarlo desde la app.
        if (userId == actingUserId && request.Role != RoleCode.Admin)
            return Result.Failure<UserSummaryResponse>(
                Error.Validation("Role", "No puedes quitarte tu propio rol de Administrador."));

        // Carrera obligatoria para Student, Teacher y Coordinator; opcional para el resto.
        var careerRequired = request.Role is RoleCode.Student or RoleCode.Teacher or RoleCode.Coordinator;
        var hasCareer = request.CareerId is not null && request.CareerId != Guid.Empty;

        if (careerRequired && !hasCareer)
            return Result.Failure<UserSummaryResponse>(
                Error.Validation("CareerId", "Debes seleccionar una carrera para este rol."));

        if (hasCareer && await careerRepository.GetByIdAsync(request.CareerId!.Value, ct) is null)
            return Result.Failure<UserSummaryResponse>(
                Error.Validation("CareerId", "La carrera seleccionada no existe."));

        var role = await roleRepository.GetByCodeAsync(request.Role, ct);
        if (role is null)
            return Result.Failure<UserSummaryResponse>(RoleErrors.NotFound);

        var profileResult = user.UpdateProfile(request.Name, request.Phone);
        if (profileResult.IsFailure)
            return Result.Failure<UserSummaryResponse>(profileResult.Error);

        var roleResult = user.ChangeRole(role.Id);
        if (roleResult.IsFailure)
            return Result.Failure<UserSummaryResponse>(roleResult.Error);

        if (hasCareer)
        {
            var careerResult = user.AssignCareer(request.CareerId!.Value);
            if (careerResult.IsFailure)
                return Result.Failure<UserSummaryResponse>(careerResult.Error);
        }
        else
        {
            user.ClearCareer();
        }

        userRepository.Update(user);
        await unitOfWork.SaveChangesAsync(ct);

        // Se usa role.Name (el nuevo), no user.Role.Name: esa propiedad
        // todavía podría traer el rol anterior cargado en memoria.
        return new UserSummaryResponse(user.Id, user.Name, user.Email.Value, role.Name, user.CareerId);
    }
}