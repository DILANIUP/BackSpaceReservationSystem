using SpaceReservationSystem.Domain.Errors;
using SpaceReservationSystem.Domain.Primitives;
using SpaceReservationSystem.Domain.ValueObjects;

namespace SpaceReservationSystem.Domain.Entities;

public class User : AuditableEntity
{
    public string Name { get; private set; } = null!;
    public Email Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string Phone { get; private set; } = null!;
    public Guid RoleId { get; private set; }
    public Role Role { get; private set; } = null!; //* es nullable porque no todos los roles necesariamente tienen asignacion
    public Guid? CareerId { get; private set; }
    public Career? Career { get; private set; }  
    public ICollection<Reservation> Reservations { get; private set; } = new List<Reservation>();
    public ICollection<ReservationHistory> ReservationHistories { get; set; } = new List<ReservationHistory>();

    private User(Guid id, string name, Email email, string passwordHash, string phone, Guid roleId, Guid? careerId) 
        : base(id)
    {
        Name = name;
        Email = email;
        PasswordHash = passwordHash;
        Phone = phone;
        RoleId = roleId;
        CareerId = careerId;
    }

    private User (){ }


    public static Result<User> Create(
        string name,
        Email email,
        string passwordHash,
        string phone,
        Guid roleId,
        Guid? careerId = null
    )
    {
        if(string.IsNullOrWhiteSpace(name))
            return Result.Failure<User>(UserErrors.InvalidName);

        if(string.IsNullOrWhiteSpace(email.Value))
            return Result.Failure<User>(UserErrors.InvalidEmail);

        if(string.IsNullOrWhiteSpace(phone))
            return Result.Failure<User>(UserErrors.InvalidPhone);

        if(roleId == Guid.Empty)
            return Result.Failure<User>(UserErrors.InvalidRole);
                
        return new User(Guid.NewGuid(), name.Trim(), email,passwordHash, phone.Trim(), roleId, careerId);

    }

    public Result UpdateProfile(string name, string phone)
    {
        if(string.IsNullOrWhiteSpace(name))
            return Result.Failure(UserErrors.InvalidName);

        if(string.IsNullOrWhiteSpace(phone))
            return Result.Failure(UserErrors.InvalidPhone);

        Name = name.Trim();
        Phone = phone.Trim();
        return Result.Success();
    }

    public Result AssignCareer(Guid careerId)
    {
        if(careerId == Guid.Empty)
            return Result.Failure(UserErrors.InvalidCareer);

        CareerId = careerId;
        return Result.Success();
    }

    public Result ChangePassword(string newPasswordHash)
    {
        if(string.IsNullOrWhiteSpace(newPasswordHash))
            return Result.Failure(UserErrors.InvalidPassword);

        PasswordHash = newPasswordHash;
        return Result.Success();
    }
    

} 