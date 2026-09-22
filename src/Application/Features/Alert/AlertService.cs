using AlertEntity = SpaceReservationSystem.Domain.Entities.Alert;
using SpaceReservationSystem.Domain.Enums;
using SpaceReservationSystem.Domain.Interfaces;
using SpaceReservationSystem.Domain.Primitives;

namespace SpaceReservationSystem.Application.Features.Alert;

public class AlertService
{
    private readonly IAlertRepository _alertRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AlertService(IAlertRepository alertRepository, IUnitOfWork unitOfWork)
    {
        _alertRepository = alertRepository;
        _unitOfWork = unitOfWork;
    }

    // Busca una alerta por Id
    public async Task<Result<AlertEntity>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var alert = await _alertRepository.GetByIdAsync(id, ct);

        if (alert is null)
            return Result.Failure<AlertEntity>(Error.NotFound("Alert", id.ToString()));

        return alert;
    }

    // Crea una alerta nueva
    public async Task<Result<AlertEntity>> CreateAsync(
        AlertType type, string description, Guid? resourceId, Guid? spaceId, CancellationToken ct = default)
    {
        var result = AlertEntity.Create(type, description, resourceId, spaceId);

        if (result.IsFailure)
            return Result.Failure<AlertEntity>(result.Error); 

        _alertRepository.Add(result.Value);
        await _unitOfWork.SaveChangesAsync(ct); // aquí se guarda en la BD

        return result.Value;
    }

    // Marca una alerta como resuelta 
    public async Task<Result> ResolveAsync(Guid id, CancellationToken ct = default)
    {
        var alert = await _alertRepository.GetByIdAsync(id, ct);

        if (alert is null)
            return Result.Failure(Error.NotFound("Alert", id.ToString()));

        var result = alert.Resolve(); 

        if (result.IsFailure)
            return result;

        _alertRepository.Update(alert);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<List<AlertEntity>> GetAllAsync(CancellationToken ct = default)
    => await _alertRepository.GetAllAsync(ct);
}