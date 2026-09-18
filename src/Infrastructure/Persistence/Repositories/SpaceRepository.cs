using Microsoft.EntityFrameworkCore;
using SpaceReservationSystem.Domain.Entities;
using SpaceReservationSystem.Domain.Interfaces;
using SpaceReservationSystem.Infrastructure.Data;

namespace SpaceReservationSystem.Infrastructure.Persistence.Repositories;

public class SpaceRepository : ISpaceRepository
{
    private readonly AppDbContext _context;

    public SpaceRepository(AppDbContext context) => _context = context;

    public async Task<Space?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Spaces.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IEnumerable<Space>> GetAllAsync(CancellationToken ct = default)
        => await _context.Spaces.ToListAsync(ct);

    public void Add(Space space) => _context.Spaces.Add(space);

    public void Update(Space space) => _context.Spaces.Update(space);
}