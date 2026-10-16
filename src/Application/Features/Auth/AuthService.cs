using SpaceReservationSystem.Domain.Entities;
using SpaceReservationSystem.Domain.Enums;
using SpaceReservationSystem.Domain.Errors;
using SpaceReservationSystem.Domain.Interfaces;
using SpaceReservationSystem.Domain.Primitives;
using SpaceReservationSystem.Infrastructure.Authentication;

using System.Text.RegularExpressions;

using EmailValueObject = SpaceReservationSystem.Domain.ValueObjects.Email;

namespace SpaceReservationSystem.Application.Features.Auth;

public class AuthService(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    ICareerRepository careerRepository,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    ITokenService tokenService
)
{
    // Dominio de tu compañera. Debe coincidir con el que arma el frontend
    // (register.component.ts y el modal de user-list.ts).
    private const string InstitutionalDomain = "@unibe.edu.ec";
    private static readonly Regex NameRegex = new(@"^[\p{L}\s'.-]+$");
    private static readonly Regex PhoneRegex = new(@"^\d{10}$");

    // Validaciones estrictas (de tu compa�era) aplicadas a AMBOS caminos:
    // registro p�blico y creaci�n por Admin. As� un Admin no puede crear
    // usuarios con datos que el registro p�blico rechazar�a.
    private static (string Field, string Message)? ValidateRegister(RegisterRequest r, bool isAdminCreation = false)
    {
        var name = r.Name?.Trim() ?? "";
        if (name.Length < 3 || name.Length > 150 || !NameRegex.IsMatch(name))
            return ("Name", "El nombre debe tener entre 3 y 150 letras.");

        var email = r.Email?.Trim() ?? "";
        if (email.Length <= InstitutionalDomain.Length ||
            !email.EndsWith(InstitutionalDomain, StringComparison.OrdinalIgnoreCase))
            return ("Email", $"Usa tu correo institucional ({InstitutionalDomain}).");

        if (!PhoneRegex.IsMatch(r.Phone ?? ""))
            return ("Phone", "El tel�fono debe tener exactamente 10 d�gitos.");

        if (!Regex.IsMatch(r.IdentificationNumber ?? "", @"^\d{10}$"))
            return ("IdentificationNumber", "La c�dula debe tener exactamente 10 d�gitos.");

        var pwd = r.Password ?? "";
        if (pwd.Length < 8 || !pwd.Any(char.IsLetter) || !pwd.Any(char.IsDigit))
            return ("Password", "La contraseña debe tener 8 caracteres, con letras y n�meros.");

        // Tuyo: solo el registro público se limita a Student/Teacher.
        // El Admin puede crear cualquier rol.
        if (!isAdminCreation && r.RequestedRole != RoleCode.Student && r.RequestedRole != RoleCode.Teacher)
            return ("RequestedRole", "Solo puedes registrarte como Estudiante o Docente.");

        // Tuyo: la carrera solo es obligatoria para Student, Teacher y Coordinator.
        // Vicerrector, Bienes y Admin pueden no tener carrera.
        var careerRequired = r.RequestedRole is RoleCode.Student or RoleCode.Teacher or RoleCode.Coordinator;
        if (careerRequired && (r.CareerId is null || r.CareerId == Guid.Empty))
            return ("CareerId", "Debes seleccionar una carrera.");

        return null;
    }

    // L�gica com�n de crear un usuario: la usan el registro p�blico y la
    // creaci�n por Admin. Devuelve el usuario y su rol (el rol hace falta para el token).
    private async Task<Result<(Domain.Entities.User User, Role Role)>> CreateUserAsync(
        RegisterRequest request, bool isAdminCreation, CancellationToken ct)
    {
        var invalid = ValidateRegister(request, isAdminCreation);
        if (invalid is not null)
            return Result.Failure<(Domain.Entities.User User, Role Role)>(
                Error.Validation(invalid.Value.Field, invalid.Value.Message));

        var emailResult = EmailValueObject.Create(request.Email);
        if (emailResult.IsFailure)
            return Result.Failure<(Domain.Entities.User User, Role Role)>(emailResult.Error);

        if (await userRepository.ExistsByEmailAsync(emailResult.Value, ct))
            return Result.Failure<(Domain.Entities.User User, Role Role)>(UserErrors.EmailAlreadyExists);

        // De tu compa�era: la c�dula es �nica.
        if (await userRepository.ExistsByIdentificationNumberAsync(request.IdentificationNumber, ct))
            return Result.Failure<(Domain.Entities.User User, Role Role)>(
                Error.Validation("IdentificationNumber", "La c�dula ya est� registrada."));

        // La carrera es opcional seg�n el rol, as� que solo se verifica si vino.
        if (request.CareerId is not null && request.CareerId != Guid.Empty
            && await careerRepository.GetByIdAsync(request.CareerId.Value, ct) is null)
            return Result.Failure<(Domain.Entities.User User, Role Role)>(
                Error.Validation("CareerId", "La carrera seleccionada no existe."));

        var role = await roleRepository.GetByCodeAsync(request.RequestedRole, ct);
        if (role is null)
            return Result.Failure<(Domain.Entities.User User, Role Role)>(RoleErrors.NotFound);

        var passwordHash = passwordHasher.Hash(request.Password);

        var userResult = Domain.Entities.User.Create(
            request.Name, emailResult.Value, passwordHash, request.Phone,
            request.IdentificationNumber, role.Id, request.CareerId);
        if (userResult.IsFailure)
            return Result.Failure<(Domain.Entities.User User, Role Role)>(userResult.Error);

        userRepository.Add(userResult.Value);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success((userResult.Value, role));
    }

    // Registro p�blico: delega en CreateUserAsync y adem�s genera los tokens.
    public async Task<Result<RegisterResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var created = await CreateUserAsync(request, isAdminCreation: false, ct);
        if (created.IsFailure)
            return Result.Failure<RegisterResponse>(created.Error);

        var (user, role) = created.Value;
        var accessToken = tokenService.GenerateAccessToken(user, role);
        var refreshToken = tokenService.GenerateRefreshToken();

        return new RegisterResponse(user.Id, accessToken, refreshToken);
    }

    // Creaci�n por Admin. No genera tokens (el Admin no se loguea como el
    // usuario nuevo), solo devuelve el id.
    public async Task<Result<Guid>> CreateByAdminAsync(RegisterRequest request, CancellationToken ct)
    {
        var created = await CreateUserAsync(request, isAdminCreation: true, ct);
        return created.IsFailure
            ? Result.Failure<Guid>(created.Error)
            : Result.Success(created.Value.User.Id);
    }

    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var emailResult = EmailValueObject.Create(request.Email);
        if (emailResult.IsFailure)
            return Result.Failure<LoginResponse>(UserErrors.InvalidEmail);

        var user = await userRepository.GetByEmailAsync(emailResult.Value, ct); // ya incluye Role
        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
            return Result.Failure<LoginResponse>(
                Error.Validation("Credentials", "Correo o contrase�a incorrectos."));

        // Tuyo: se revisa DESPU�S de verificar la contrase�a, para no revelar
        // si una cuenta existe a quien no conoce la clave.
        if (!user.IsActive)
            return Result.Failure<LoginResponse>(
                Error.Validation("Account", "Tu cuenta est� desactivada. Contacta al administrador."));

        var role = await roleRepository.GetByIdAsync(user.RoleId, ct);
        if (role is null)
            return Result.Failure<LoginResponse>(RoleErrors.NotFound);

        var accessToken = tokenService.GenerateAccessToken(user, role);
        var refreshToken = tokenService.GenerateRefreshToken();

        return new LoginResponse(user.Id, accessToken, refreshToken);
    }
}