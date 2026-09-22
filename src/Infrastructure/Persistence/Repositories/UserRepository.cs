using Microsoft.EntityFrameworkCore;
using SpaceReservationSystem.Domain.Entities;
using SpaceReservationSystem.Domain.Interfaces;
using SpaceReservationSystem.Domain.ValueObjects;
using SpaceReservationSystem.Infrastructure.Data;

namespace SpaceReservationSystem.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context) => _context = context;

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) // Busca el usuarioy si no existe devuelve null
        => await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<User?> GetByEmailAsync(Email email, CancellationToken ct = default)  
        => await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email.Value == email.Value, ct);

    public async Task<bool> ExistsByEmailAsync(Email email, CancellationToken ct = default)
        => await _context.Users
            .AnyAsync(u => u.Email.Value == email.Value, ct);

    public void Add(User user) => _context.Users.Add(user); // Marca el usuario como nuevo , pendiente de guardar en memoria

    public void Update(User user) => _context.Users.Update(user);
}