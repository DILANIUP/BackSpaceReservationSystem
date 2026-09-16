using ResourceEntity = SpaceReservationSystem.Domain.Entities.Resource;
using SpaceReservationSystem.Domain.Interfaces;
using SpaceReservationSystem.Domain.Primitives;

namespace SpaceReservationSystem.Application.Features.Resource;

public class ResourceService
{
    private readonly IResourceRepository _resourceRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ResourceService(
        IResourceRepository resourceRepository,
        IUnitOfWork unitOfWork)
    {
        _resourceRepository = resourceRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ResourceEntity>> GetByIdAsync(
        Guid id,
        CancellationToken ct = default)
    {
        var resource = await _resourceRepository.GetByIdAsync(id, ct);

        if (resource is null)
            return Result.Failure<ResourceEntity>(
                Error.NotFound("Resource", id.ToString()));

        return resource;
    }

    public async Task<IEnumerable<ResourceEntity>> ListActiveAsync(
        CancellationToken ct = default
    ) => await _resourceRepository.GetAllActiveAsync(ct);

    public async Task<Result<ResourceEntity>> CreateAsync(
        string name,
        string? description,
        int availableQuantity,
        CancellationToken ct = default)
    {
        var result = ResourceEntity.Create(name, description, availableQuantity);

        if (result.IsFailure)
            return Result.Failure<ResourceEntity>(result.Error);

        _resourceRepository.Add(result.Value);

        await _unitOfWork.SaveChangesAsync(ct);

        return result.Value;
    }

    public async Task<Result> UpdateAsync(
        Guid id,
        string name,
        string? description,
        int availableQuantity,
        CancellationToken ct = default)
    {
        var resource = await _resourceRepository.GetByIdAsync(id, ct);

        if (resource is null)
            return Result.Failure(
                Error.NotFound("Resource", id.ToString()));

        var result = resource.Update(name, description, availableQuantity);

        if (result.IsFailure)
            return result;

        _resourceRepository.Update(resource);

        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result> ActivateAsync(
        Guid id,
        CancellationToken ct = default)
    {
        var resource = await _resourceRepository.GetByIdAsync(id, ct);

        if (resource is null)
            return Result.Failure(
                Error.NotFound("Resource", id.ToString()));

        var result = resource.Activate();

        if (result.IsFailure)
            return result;

        _resourceRepository.Update(resource);

        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result> DeactivateAsync(
        Guid id,
        CancellationToken ct = default)
    {
        var resource = await _resourceRepository.GetByIdAsync(id, ct);

        if (resource is null)
            return Result.Failure(
                Error.NotFound("Resource", id.ToString()));

        var result = resource.Deactivate();

        if (result.IsFailure)
            return result;

        _resourceRepository.Update(resource);

        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}