using SpaceEntity = SpaceReservationSystem.Domain.Entities.Space;
using SpaceReservationSystem.Domain.Enums;
using SpaceReservationSystem.Domain.Interfaces;
using SpaceReservationSystem.Domain.Primitives;

namespace SpaceReservationSystem.Application.Features.Space;

public class SpaceService
{
    private readonly ISpaceRepository _spaceRepository; //consulta, agrega y actualiza espacios de acceso a la BD
    private readonly IUnitOfWork _unitOfWork; // persiste canbios

    // Constructor del servicio
    public SpaceService(
        ISpaceRepository spaceRepository,
        IUnitOfWork unitOfWork)
    {
        _spaceRepository = spaceRepository;
        _unitOfWork = unitOfWork;
    }
    
    // Obtencion de un espacio por ID
    public async Task<Result<SpaceEntity>> GetByIdAsync(
        Guid id,
        CancellationToken ct = default)
    {
        var space = await _spaceRepository.GetByIdAsync(id, ct);

        if (space is null)
            return Result.Failure<SpaceEntity>(
                Error.NotFound("Space", id.ToString()));

        return space;
    }

    public async Task<IEnumerable<SpaceEntity>> ListActiveAsync(
        CancellationToken ct = default
    ) => await _spaceRepository.GetAllActiveAsync(ct);

    // Creacion de un espacio
    public async Task<Result<SpaceEntity>> CreateAsync(
        string name,
        SpaceType type,
        int capacity,
        string location,
        CancellationToken ct = default)
    {
        var result = SpaceEntity.Create(name, type, capacity, location);

        if (result.IsFailure)
            return Result.Failure<SpaceEntity>(result.Error);

        _spaceRepository.Add(result.Value);

        await _unitOfWork.SaveChangesAsync(ct);

        return result.Value;
    }

    // Actualiza un espacio
    public async Task<Result> UpdateAsync(
        Guid id,
        string name,
        int capacity,
        string location,
        CancellationToken ct = default)
    {
        var space = await _spaceRepository.GetByIdAsync(id, ct);

        if (space is null)
            return Result.Failure(
                Error.NotFound("Space", id.ToString()));

        var result = space.Update(name, capacity, location);

        if (result.IsFailure)
            return result;

        _spaceRepository.Update(space);

        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    // Delegan los metodos del dominio (No permite activar un espacio que ya esta activo) - Activa un espacio
    public async Task<Result> ActivateAsync(
        Guid id,
        CancellationToken ct = default)
    {
        var space = await _spaceRepository.GetByIdAsync(id, ct);

        if (space is null)
            return Result.Failure(
                Error.NotFound("Space", id.ToString()));

        var result = space.Activate();

        if (result.IsFailure)
            return result;

        _spaceRepository.Update(space);

        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    // Desactiva un espacio
    public async Task<Result> DeactivateAsync(
        Guid id,
        CancellationToken ct = default)
    {
        var space = await _spaceRepository.GetByIdAsync(id, ct);

        if (space is null)
            return Result.Failure(
                Error.NotFound("Space", id.ToString()));

        var result = space.Deactivate();

        if (result.IsFailure)
            return result;

        _spaceRepository.Update(space);

        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}