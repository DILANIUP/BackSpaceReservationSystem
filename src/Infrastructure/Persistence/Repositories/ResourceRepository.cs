using Microsoft.EntityFrameworkCore;
using SpaceReservationSystem.Domain.Entities;
using SpaceReservationSystem.Domain.Interfaces;
using SpaceReservationSystem.Infrastructure.Data;

namespace SpaceReservationSystem.Infrastructure.Persistence.Repositories;

public class ResourceRepository : IResourceRepository
{
    private readonly AppDbContext _context;

    public ResourceRepository(AppDbContext context) => _context = context;

    public async Task<Resource?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Resources.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IEnumerable<Resource>> GetAllActiveAsync(CancellationToken ct = default)
        => await _context.Resources.Where(r => r.Status).ToListAsync(ct);
    public void Add(Resource resource) => _context.Resources.Add(resource);

    public void Update(Resource resource) => _context.Resources.Update(resource);
}