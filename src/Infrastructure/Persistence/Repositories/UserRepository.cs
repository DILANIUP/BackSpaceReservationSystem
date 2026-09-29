using Microsoft.EntityFrameworkCore;
using SpaceReservationSystem.Domain.Entities;
using SpaceReservationSystem.Domain.Enums;
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

    // NUEVO: alimenta el selector "¿Para quién es esta reserva?" en el
    // frontend. role/careerId son opcionales — null significa "sin filtrar por
    // eso". Coordinator siempre va a mandar los dos (Student/Teacher + su
    // propia carrera); Vicerrector/Bienes/Admin pueden mandar solo role, o nada.
    public async Task<List<User>> SearchAsync (RoleCode? role, Guid? careerId, CancellationToken ct)
    {
        var query = _context.Users
            .Include(u => u.Role)
            .Include(u => u.Career)
            .AsQueryable();

        if (role is not null)
            query = query.Where(u => u.Role!.Code == role);
        
        if (careerId is not null)
            query = query.Where(u => u.CareerId == careerId);

        return await query.OrderBy(u => u.Name).ToListAsync(ct);
    }
    public async Task<List<User>> GetByRoleAsync(RoleCode role, CancellationToken ct = default)
    {
        return await _context.Users
            .Include(u => u.Role)
            .Where(u => u.Role!.Code == role)
            .ToListAsync(ct);
    }
    public void Add(User user) => _context.Users.Add(user); // Marca el usuario como nuevo , pendiente de guardar en memoria

    public void Update(User user) => _context.Users.Update(user);
}