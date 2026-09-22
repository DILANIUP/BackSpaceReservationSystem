using SpaceReservationSystem.Application.Features.User;
using SpaceReservationSystem.Domain.Errors;
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
}