
using SpaceReservationSystem.Domain.Primitives;

namespace SpaceReservationSystem.Domain.Errors;

public static class RoleErrors
{
    public static readonly Error InvalidName = new("Role.InvalidName", "El nombre del rol no es válido.");
    public static readonly Error NotFound = new("Role.NotFound", "No se encontró el rol.");
}

public static class FacultyErrors
{
    public static readonly Error InvalidName = new("Faculty.InvalidName", "El nombre de la facultad no es válido.");
}

public static class CareerErrors
{
    public static readonly Error InvalidName = new("Career.InvalidName", "El nombre de la carrera no es válido.");
    public static readonly Error InvalidFaculty = new("Career.InvalidFaculty", "Se requiere un identificador de facultad válido.");
}

public static class UserErrors
{
    public static readonly Error InvalidName = new("User.InvalidName", "El nombre no es válido.");
    public static readonly Error InvalidEmail = new("User.InvalidEmail", "El correo electrónico no es válido.");
    public static readonly Error InvalidPhone = new("User.InvalidPhone", "El teléfono no es válido.");
    public static readonly Error InvalidRole = new("User.InvalidRole", "El rol no es válido.");
    public static readonly Error InvalidCareer = new("User.InvalidCareer", "Debes seleccionar una carrera.");
    public static readonly Error InvalidPassword = new("User.InvalidPassword", "La contraseña no es válida.");
    public static readonly Error NotFound = new("Role.NotFound", "No se encontró el usuario.");
    public static readonly Error EmailAlreadyExists = new("User.EmailAlreadyExists", "Este correo electrónico ya está registrado.");
}

public static class SpaceErrors
{
    public static readonly Error InvalidName = new("Space.InvalidName", "El nombre del espacio no es válido.");
    public static readonly Error InvalidLocation = new("Space.InvalidLocation", "La ubicación del espacio no es válida.");
    public static readonly Error InvalidCapacity = new("Space.InvalidCapacity", "La capacidad debe ser mayor que 0 y no superar las 30 personas.");
    public static readonly Error AlreadyActive = new("Space.AlreadyActive", "El espacio ya está activo.");
    public static readonly Error AlreadyInactive = new("Space.AlreadyInactive", "El espacio ya está inactivo.");
}

public static class ResourceErrors
{
    public static readonly Error InvalidName = new("Resource.InvalidName", "El nombre del recurso no es válido.");
    public static readonly Error InvalidQuantity = new("Resource.InvalidQuantity", "La cantidad disponible no puede ser negativa.");
    public static readonly Error AlreadyActive = new("Resource.AlreadyActive", "El recurso ya está activo.");
    public static readonly Error AlreadyInactive = new("Resource.AlreadyInactive", "El recurso ya está inactivo.");
}

public static class ReservationErrors
{
    public static readonly Error InvalidDate = new("Reservation.InvalidDate", "La fecha de la reserva no puede ser anterior a la fecha actual.");
    public static readonly Error InvalidTimeRange = new("Reservation.InvalidTimeRange", "La hora de finalización debe ser posterior a la hora de inicio.");
    public static readonly Error OutsideAllowedHours = new("Reservation.OutsideAllowedHours", "Las reservas solo están permitidas entre las 7:00 y las 23:00.");
    public static readonly Error InvalidReason = new("Reservation.InvalidReason", "El motivo de la reserva es obligatorio.");
    public static readonly Error InvalidUser = new("Reservation.InvalidUser", "El usuario no es válido.");
    public static readonly Error InsufficientNotice = new("Reservation.InsufficientNotice", "Las reservas deben realizarse con al menos 72 horas de anticipación.");
    public static readonly Error InvalidStatusTransition = new("Reservation.InvalidStatusTransition", "La reserva no puede cambiar al estado solicitado desde su estado actual.");
    public static readonly Error SlotAlreadyTaken = new("Reservation.SlotAlreadyTaken", "El espacio ya está reservado para ese horario.");
}

public static class ReservationResourceErrors
{
    public static readonly Error InvalidQuantity = new("ReservationResource.InvalidQuantity", "La cantidad solicitada debe ser mayor que 0.");
    public static readonly Error InvalidReservation = new("ReservationResource.InvalidReservation", "La reserva no es válida.");
    public static readonly Error InvalidResource = new("ReservationResource.InvalidResource", "El recurso no es válido.");
}

public static class ReservationHistoryErrors
{
    public static readonly Error InvalidJustification = new("ReservationHistory.InvalidJustification", "La justificación es obligatoria.");
    public static readonly Error InvalidReservation = new("ReservationHistory.InvalidReservation", "La reserva no es válida.");
    public static readonly Error InvalidChangedBy = new("ReservationHistory.InvalidChangedBy", "El usuario que realizó el cambio no es válido.");
}

public static class AlertErrors
{
    public static readonly Error InvalidDescription = new("Alert.InvalidDescription", "La descripción es obligatoria.");
    public static readonly Error MissingTarget = new("Alert.MissingTarget", "La alerta debe estar asociada a un recurso o a un espacio.");
    public static readonly Error AlreadyResolved = new("Alert.AlreadyResolved", "La alerta ya está resuelta.");
    public static readonly Error InvalidObservation = new("Alert.InvalidObservation", "La observación de resolución es obligatoria.");
}

public static class EmailTemplateErrors
{
    public static readonly Error InvalidCode = new("EmailTemplate.InvalidCode", "El código de la plantilla es obligatorio.");
    public static readonly Error InvalidSubject = new("EmailTemplate.InvalidSubject", "El asunto es obligatorio.");
    public static readonly Error InvalidBody = new("EmailTemplate.InvalidBody", "El contenido de la plantilla es obligatorio.");
}

public static class EmailLogErrors
{
    public static readonly Error InvalidEmail = new("EmailLog.InvalidEmail", "El correo electrónico del destinatario es obligatorio.");
    public static readonly Error InvalidTemplate = new("EmailLog.InvalidTemplate", "Se requiere un identificador de plantilla válido.");
    public static readonly Error InvalidErrorMessage = new("EmailLog.InvalidErrorMessage", "El mensaje de error es obligatorio cuando se marca un envío como fallido.");
}

public static class VoucherErrors
{
    public static readonly Error InvalidPath = new("Voucher.InvalidPath", "La ruta del archivo PDF es obligatoria.");
    public static readonly Error InvalidReservation = new("Voucher.InvalidReservation", "Se requiere un identificador de reserva válido.");
}
