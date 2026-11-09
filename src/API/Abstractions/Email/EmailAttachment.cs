namespace SpaceReservationSystem.API.Abstractions.Email;

//envia el comprobante PDF en el correo
public sealed record EmailAttachment(
    string FileName,
    byte[] Content,
    string ContentType
);