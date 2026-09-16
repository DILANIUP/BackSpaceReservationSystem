using Microsoft.EntityFrameworkCore;
using SpaceReservationSystem.Domain.Entities;
using SpaceReservationSystem.Domain.Interfaces;
using SpaceReservationSystem.Infrastructure.Data;

namespace SpaceReservationSystem.Infrastructure.Persistence.Repositories;

public class CareerRepository : ICareerRepository
{
    private readonly AppDbContext _context;

    public CareerRepository(AppDbContext context) => _context = context;
    public async Task<List<Career>> GetAllAsync(CancellationToken ct = default)
    => await _context.Careers.ToListAsync(ct);

    public async Task<Career?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Careers.FirstOrDefaultAsync(c => c.Id == id, ct);

    public void Add(Career career) => _context.Careers.Add(career);

    public void Update(Career career) => _context.Careers.Update(career);
}