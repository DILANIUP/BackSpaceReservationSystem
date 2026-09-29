using SpaceReservationSystem.Domain.Entities;
using SpaceReservationSystem.Domain.Enums;
using SpaceReservationSystem.Domain.ValueObjects;

namespace SpaceReservationSystem.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(Email email, CancellationToken ct = default);
    Task<List<User>> SearchAsync(RoleCode? role, Guid? careerId, CancellationToken ct = default);
    Task<List<User>> GetByRoleAsync(RoleCode role, CancellationToken ct = default);
    void Add(User user);
    void Update(User user);
}