using CareerEntity = SpaceReservationSystem.Domain.Entities.Career;
using SpaceReservationSystem.Domain.Interfaces;
using SpaceReservationSystem.Domain.Primitives;

namespace SpaceReservationSystem.Application.Features.Career;

public class CareerService
{
    private readonly ICareerRepository _careerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CareerService(
        ICareerRepository careerRepository,
        IUnitOfWork unitOfWork)
    {
        _careerRepository = careerRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CareerEntity>> GetByIdAsync(
        Guid id,
        CancellationToken ct = default)
    {
        var career = await _careerRepository.GetByIdAsync(id, ct);

        if (career is null)
            return Result.Failure<CareerEntity>(
                Error.NotFound("Career", id.ToString()));

        return career;
    }

    public async Task<Result<CareerEntity>> CreateAsync(
        string name,
        Guid facultyId,
        CancellationToken ct = default)
    {
        var result = CareerEntity.Create(name, facultyId);

        if (result.IsFailure)
            return Result.Failure<CareerEntity>(result.Error);

        _careerRepository.Add(result.Value);

        await _unitOfWork.SaveChangesAsync(ct);

        return result.Value;
    }

    public async Task<Result> UpdateAsync(
        Guid id,
        string name,
        CancellationToken ct = default)
    {
        var career = await _careerRepository.GetByIdAsync(id, ct);

        if (career is null)
            return Result.Failure(
                Error.NotFound("Career", id.ToString()));

        var result = career.Update(name);

        if (result.IsFailure)
            return result;

        _careerRepository.Update(career);

        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<List<CareerEntity>> GetAllAsync(CancellationToken ct = default)
    => await _careerRepository.GetAllAsync(ct);
}