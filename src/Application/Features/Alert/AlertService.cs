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
    private readonly INotificationRepository _notificationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;

    public AlertService(
        IAlertRepository alertRepository,
        INotificationRepository notificationRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IUserRepository userRepository)
    {
        _alertRepository = alertRepository;
        _notificationRepository = notificationRepository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _userRepository = userRepository;
    }

    // Busca el nombre del usuario (null si no hay usuario)
    private async Task<string?> GetUserNameAsync(Guid? userId, CancellationToken ct)
    {
        if (!userId.HasValue)
            return null;

        var user = await _userRepository.GetByIdAsync(userId.Value, ct);
        return user?.Name;
    }

    // Convierte la entidad en la respuesta de la API
    private static AlertResponse ToResponse(AlertEntity a, string? createdByName) =>
        new(
            a.Id,
            a.Type,
            a.Description,
            a.CreatedAt,
            a.CreatedBy,
            createdByName,
            a.ResolvedAt,
            a.IsResolved,
            a.ResolutionObservation,
            a.ResourceId,
            a.SpaceId);

    public async Task<Result<AlertResponse>> GetByIdAsync(
        Guid id,
        CancellationToken ct = default)
    {
        var alert = await _alertRepository.GetByIdAsync(id, ct);

        if (alert is null)
            return Result.Failure<AlertResponse>(
                Error.NotFound("Alert", id.ToString()));

        var name = await GetUserNameAsync(alert.CreatedBy, ct);
        return ToResponse(alert, name);
    }

    // Crea una alerta nueva
    public async Task<Result<AlertResponse>> CreateAsync(
        AlertType type,
        string description,
        Guid? resourceId,
        Guid? spaceId,
        CancellationToken ct = default)
    {
        var result = AlertEntity.Create(type, description, resourceId, spaceId);

        if (result.IsFailure)
            return Result.Failure<AlertResponse>(result.Error);

        _alertRepository.Add(result.Value);

        // Notificar a Bienes
        var bienesUsers = await _userRepository.GetByRoleAsync(RoleCode.Bienes, ct);

        foreach (var user in bienesUsers)
        {
            var notification = NotificationEntity.Create(
                user.Id,
                result.Value.Id,
                $"Nueva alerta reportada: {result.Value.Description}");

            _notificationRepository.Add(notification);
        }

        // Notificar a Admin
        var adminUsers = await _userRepository.GetByRoleAsync(RoleCode.Admin, ct);

        foreach (var user in adminUsers)
        {
            var notification = NotificationEntity.Create(
                user.Id,
                result.Value.Id,
                $"Nueva alerta reportada: {result.Value.Description}");

            _notificationRepository.Add(notification);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        var name = await GetUserNameAsync(result.Value.CreatedBy, ct);
        return ToResponse(result.Value, name);
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

        // Notificar al usuario que reportó la alerta
        if (alert.CreatedBy.HasValue)
        {
            var notification = NotificationEntity.Create(
                alert.CreatedBy.Value,
                alert.Id,
                $"Tu reporte fue atendido. {alert.ResolutionObservation}");

            _notificationRepository.Add(notification);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    // Obtiene las alertas visibles para el usuario
    public async Task<List<AlertResponse>> GetAllAsync(CancellationToken ct = default)
    {
        var role = _currentUserService.Role;
        var userId = _currentUserService.UserId;

        List<AlertEntity> alerts;

        if (role is "Bienes" or "Admin")
            alerts = await _alertRepository.GetAllAsync(ct);
        else if (userId.HasValue)
            alerts = await _alertRepository.GetByCreatedByAsync(userId.Value, ct);
        else
            return new List<AlertResponse>();

        // Cache para no consultar dos veces al mismo usuario
        var names = new Dictionary<Guid, string?>();
        var result = new List<AlertResponse>();

        foreach (var alert in alerts)
        {
            string? name = null;

            if (alert.CreatedBy.HasValue &&
                !names.TryGetValue(alert.CreatedBy.Value, out name))
            {
                name = await GetUserNameAsync(alert.CreatedBy, ct);
                names[alert.CreatedBy.Value] = name;
            }

            result.Add(ToResponse(alert, name));
        }

        return result;
    }
}