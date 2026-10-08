
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
    private const string InstitutionalDomain = "@unibe.edu.ec";
    private static readonly Regex NameRegex = new(@"^[\p{L}\s'.-]+$");
    private static readonly Regex PhoneRegex = new(@"^\d{10}$");

    private static (string Field, string Message)? ValidateRegister(RegisterRequest r)
    {
        var name = r.Name?.Trim() ?? "";
        if (name.Length < 3 || name.Length > 150 || !NameRegex.IsMatch(name))
            return ("Name", "El nombre debe tener entre 3 y 150 letras.");

        var email = r.Email?.Trim() ?? "";
        if (email.Length <= InstitutionalDomain.Length ||
            !email.EndsWith(InstitutionalDomain, StringComparison.OrdinalIgnoreCase))
            return ("Email", $"Usa tu correo institucional ({InstitutionalDomain}).");

        if (!PhoneRegex.IsMatch(r.Phone ?? ""))
            return ("Phone", "El teléfono debe tener exactamente 10 dígitos.");

        if (!Regex.IsMatch(r.IdentificationNumber ?? "", @"^\d{10}$"))
            return ("IdentificationNumber", "La cédula debe tener exactamente 10 dígitos.");

        var pwd = r.Password ?? "";
        if (pwd.Length < 8 || !pwd.Any(char.IsLetter) || !pwd.Any(char.IsDigit))
            return ("Password", "La contraseña debe tener 8 caracteres, con letras y números.");

        if (r.RequestedRole != RoleCode.Student && r.RequestedRole != RoleCode.Teacher)
            return ("RequestedRole", "Solo puedes registrarte como Estudiante o Docente.");

        if (r.CareerId is null || r.CareerId == Guid.Empty)
            return ("CareerId", "Debes seleccionar una carrera.");

        return null;
    }
    public async Task<Result<RegisterResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var invalid = ValidateRegister(request);
        if (invalid is not null)
            return Result.Failure<RegisterResponse>(Error.Validation(invalid.Value.Field, invalid.Value.Message));

        var emailResult = EmailValueObject.Create(request.Email);
        if(emailResult.IsFailure)
            return Result.Failure<RegisterResponse>(emailResult.Error);

        if (await userRepository.ExistsByEmailAsync(emailResult.Value, ct))
            return Result.Failure<RegisterResponse>(UserErrors.EmailAlreadyExists);

        if (await userRepository.ExistsByIdentificationNumberAsync(request.IdentificationNumber, ct))
            return Result.Failure<RegisterResponse>(
                Error.Validation("IdentificationNumber", "La cédula ya está registrada."));

        if (await careerRepository.GetByIdAsync(request.CareerId!.Value, ct) is null)
            return Result.Failure<RegisterResponse>(Error.Validation("CareerId", "La carrera seleccionada no existe."));

        var role = await roleRepository.GetByCodeAsync(request.RequestedRole, ct);
        if(role is null)
            return Result.Failure<RegisterResponse>(RoleErrors.NotFound);

        var passwordHash = passwordHasher.Hash(request.Password);

        var userResult = Domain.Entities.User.Create(request.Name,emailResult.Value,passwordHash,request.Phone,request.IdentificationNumber,role.Id,request.CareerId);
        
        if (userResult.IsFailure)
            return Result.Failure<RegisterResponse>(userResult.Error);

        var user = userResult.Value;
        userRepository.Add(user);
        await unitOfWork.SaveChangesAsync(ct);

        var accessToken = tokenService.GenerateAccessToken(user, role);
        var refreshToken = tokenService.GenerateRefreshToken();

        return new RegisterResponse(user.Id, accessToken, refreshToken);
    }

    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        // var emailResult = Email.Create(request.Email);
        var emailResult = EmailValueObject.Create(request.Email);
        if (emailResult.IsFailure)
            return Result.Failure<LoginResponse>(UserErrors.InvalidEmail);

        var user =  await userRepository.GetByEmailAsync(emailResult.Value, ct); // ya incluye Role (Include agregado en UserRepository)
        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
            return Result.Failure<LoginResponse>(Error.Validation("Credentials", "Correo o contraseña incorrectos."));

        var role = await roleRepository.GetByIdAsync(user.RoleId, ct);
        if (role is null)
            return Result.Failure<LoginResponse>(RoleErrors.NotFound);

        var accessToken = tokenService.GenerateAccessToken(user, role);
        var refreshToken = tokenService.GenerateRefreshToken();

        return new LoginResponse(user.Id, accessToken, refreshToken);
    }
}