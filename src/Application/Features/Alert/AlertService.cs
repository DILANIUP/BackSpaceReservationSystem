using SpaceReservationSystem.Domain.Entities;
using SpaceReservationSystem.Domain.Enums;
using SpaceReservationSystem.Domain.Interfaces;
using SpaceReservationSystem.Domain.Primitives;
using SpaceReservationSystem.Infrastructure.Authentication;
using AlertEntity = SpaceReservationSystem.Domain.Entities.Alert;
using NotificationEntity = SpaceReservationSystem.Domain.Entities.Notification;

namespace SpaceReservationSystem.Application.Features.Alert;

public class AlertService
{
    private readonly IAlertRepository _alertRepository;
    //private readonly IReservationRepository _reservationRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;

    public AlertService(
        IAlertRepository alertRepository,
        //IReservationRepository reservationRepository,
        INotificationRepository notificationRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IUserRepository userRepository)
    {
        _alertRepository = alertRepository;
        //_reservationRepository = reservationRepository;
        _notificationRepository = notificationRepository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _userRepository = userRepository;
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

        // Notificar a Bienes
        var bienesUsers = await _userRepository.GetByRoleAsync(
            RoleCode.Bienes,
            ct);

        foreach (var user in bienesUsers)
        {
            var notification = NotificationEntity.Create(
                user.Id,
                $"Nueva alerta reportada: {result.Value.Description}");

            _notificationRepository.Add(notification);
        }

        // Notificar a Admin
        var adminUsers = await _userRepository.GetByRoleAsync(
            RoleCode.Admin,
            ct);

        foreach (var user in adminUsers)
        {
            var notification = NotificationEntity.Create(
                user.Id,
                $"Nueva alerta reportada: {result.Value.Description}");

            _notificationRepository.Add(notification);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return result.Value;
    }

    // Marca una alerta como resuelta 
    public async Task<Result> ResolveAsync(
        Guid id,
        string observation,
        CancellationToken ct = default)
    {
        var alert = await _alertRepository.GetByIdAsync(id, ct);

        if (alert is null)
            return Result.Failure(Error.NotFound("Alert", id.ToString()));

        var result = alert.Resolve(observation);

        if (result.IsFailure)
            return result;

        _alertRepository.Update(alert);

        // Crear notificación para el usuario que reportó la alerta
        if (alert.CreatedBy.HasValue)
        {
            var notification = NotificationEntity.Create(
                alert.CreatedBy.Value,
                $"Tu reporte fue atendido. {alert.ResolutionObservation}");

            _notificationRepository.Add(notification);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<List<AlertEntity>> GetAllAsync(CancellationToken ct = default)
    {
        var role = _currentUserService.Role;
        var userId = _currentUserService.UserId;

        if (role is "Bienes" or "Admin")
            return await _alertRepository.GetPendingAsync(ct);

        if (!userId.HasValue)
            return new List<AlertEntity>();

        return await _alertRepository.GetByCreatedByAsync(userId.Value, ct);
    }
}