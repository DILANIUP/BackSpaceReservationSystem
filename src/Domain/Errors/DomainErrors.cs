using SpaceReservationSystem.Domain.Primitives;

namespace SpaceReservationSystem.Domain.Errors;

public static class RoleErrors
{
    public static readonly Error InvalidName = new("Role.InvalidName", "the name of the role is invalid");
    public static readonly Error NotFound = new("Role.NotFound", "the role was not found");
}

public static class FacultyErrors
{
    public static readonly Error InvalidName = new("Faculty.InvalidName", "the name of the faculty is invalid");
}

public static class CareerErrors
{
    public static readonly Error InvalidName = new("Career.InvalidName", "The name of the career is invalid.");
    public static readonly Error InvalidFaculty = new("Career.InvalidFaculty", "A valid faculty id is required.");
}

public static class UserErrors
{
    public static readonly Error InvalidName = new("User.InvalidName", "The name of the user is invalid.");
    public static readonly Error InvalidEmail = new("User.InvalidEmail", "The email is invalid.");
    public static readonly Error InvalidPhone = new("User.InvalidPhone", "The phone is invalid.");
    public static readonly Error InvalidRole = new("User.InvalidRole", "A valid role id is required.");
    public static readonly Error InvalidCareer = new("User.InvalidCareer", "A valid career id is required.");
    public static readonly Error InvalidPassword = new("User.InvalidPassword", "The password hash is invalid.");
    public static readonly Error NotFound = new("User.NotFound", "The user was not found.");
}

public static class SpaceErrors
{
    public static readonly Error InvalidName = new("Space.InvalidName", "The name of the space is invalid.");
    public static readonly Error InvalidLocation = new("Space.InvalidLocation", "The location of the space is invalid.");
    public static readonly Error InvalidCapacity = new("Space.InvalidCapacity", "The capacity must be greater than 0 and less than or equal to 30.");
    public static readonly Error AlreadyActive = new("Space.AlreadyActive", "The space is already active.");

    public static readonly Error AlreadyInactive = new("Space.AlreadyInactive", "The space is already inactive.");
}

public static class ResourceErrors
{
    public static readonly Error InvalidName = new("Resource.InvalidName", "The name of the resource is invalid.");
    public static readonly Error InvalidQuantity = new("Resource.InvalidQuantity", "The available quantity cannot be negative.");
    public static readonly Error AlreadyActive = new("Resource.AlreadyActive", "The resource is already active.");
    public static readonly Error AlreadyInactive = new("Resource.AlreadyInactive", "The resource is already inactive.");
}

public static class ReservationErrors
{
    public static readonly Error InvalidDate = new("Reservation.InvalidDate", "The reservation date cannot be in the past.");
    public static readonly Error InvalidTimeRange = new("Reservation.InvalidTimeRange", "The end time must be greater than the start time.");
    public static readonly Error OutsideAllowedHours = new("Reservation.OutsideAllowedHours", "Reservations are only allowed between 7:00 AM and 11:00 PM.");
    public static readonly Error InvalidReason = new("Reservation.InvalidReason", "The reason is required.");
    public static readonly Error InvalidUser = new("Reservation.InvalidUser", "The user is invalid.");
    public static readonly Error InsufficientNotice = new("Reservation.InsufficientNotice", "Reservations must be made at least 72 hours in advance.");
    public static readonly Error InvalidStatusTransition = new("Reservation.InvalidStatusTransition", "The reservation cannot transition to this status from its current state.");
    public static readonly Error SlotAlreadyTaken = new("Reservation.SlotAlreadyTaken", "The space is already reserved for that time slot.");

}

public static class ReservationResourceErrors
{
    public static readonly Error InvalidQuantity = new("ReservationResource.InvalidQuantity", "The requested quantity must be greater than 0.");
    public static readonly Error InvalidReservation = new("ReservationResource.InvalidReservation", "The reservation is invalid.");
    public static readonly Error InvalidResource = new("ReservationResource.InvalidResource", "The resource is invalid.");
}

public static class ReservationHistoryErrors
{
    public static readonly Error InvalidJustification = new("ReservationHistory.InvalidJustification", "The justification is required.");

    public static readonly Error InvalidReservation = new("ReservationHistory.InvalidReservation", "The reservation is invalid.");

    public static readonly Error InvalidChangedBy = new("ReservationHistory.InvalidChangedBy", "The user who made the change is invalid.");
}

public static class AlertErrors
{
    public static readonly Error InvalidDescription = new("Alert.InvalidDescription", "The description is required.");
    public static readonly Error MissingTarget = new("Alert.MissingTarget", "An alert must be linked to either a resource or a space.");
    public static readonly Error AlreadyResolved = new("Alert.AlreadyResolved", "The alert is already resolved.");
    public static readonly Error InvalidObservation = new("Alert.InvalidObservation", "The resolution observation is required.");
}

public static class EmailTemplateErrors
{
    public static readonly Error InvalidCode = new("EmailTemplate.InvalidCode", "The template code is required.");
    public static readonly Error InvalidSubject = new("EmailTemplate.InvalidSubject", "The subject is required.");
    public static readonly Error InvalidBody = new("EmailTemplate.InvalidBody", "The body is required.");
}

public static class EmailLogErrors
{
    public static readonly Error InvalidEmail = new("EmailLog.InvalidEmail", "The recipient email is required.");
    public static readonly Error InvalidTemplate = new("EmailLog.InvalidTemplate", "A valid template id is required.");
    public static readonly Error InvalidErrorMessage = new("EmailLog.InvalidErrorMessage", "An error message is required when marking as failed.");
}

public static class VoucherErrors
{
    public static readonly Error InvalidPath = new("Voucher.InvalidPath", "The PDF file path is required.");
    public static readonly Error InvalidReservation = new("Voucher.InvalidReservation", "A valid reservation id is required.");
}

