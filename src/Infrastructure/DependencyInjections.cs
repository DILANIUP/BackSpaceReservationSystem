using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SpaceReservationSystem.Application.Features.Alert;
using SpaceReservationSystem.Application.Features.Auth;
using SpaceReservationSystem.Application.Features.Career;
using SpaceReservationSystem.Application.Features.EmailTemplate;
using SpaceReservationSystem.Application.Features.Faculty;
using SpaceReservationSystem.Application.Features.Reservations;
using SpaceReservationSystem.Application.Features.Resource;
using SpaceReservationSystem.Application.Features.Space;
using SpaceReservationSystem.Application.Features.Users;
using SpaceReservationSystem.Application.Features.Vouchers;
using SpaceReservationSystem.Domain.Interfaces;
using SpaceReservationSystem.Infrastructure.Authentication;
using SpaceReservationSystem.Infrastructure.Data;
using SpaceReservationSystem.Infrastructure.Persistence;
using SpaceReservationSystem.Infrastructure.Persistence.Repositories;
using System.Reflection;
using System.Text;

namespace SpaceReservationSystem.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDatabase(configuration);
        services.AddRepositories();
        services.AddAuth(configuration);
        services.AddValidation();
        services.AddEmail(configuration);
        services.AddCorsPolicy(configuration);
        services.AddScoped<AuthService>();
        services.AddScoped<ReservationService>();
        return services;
    }

    private static void AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(options =>
        {
            options.AddPolicy("AngularClient", policy =>
            {
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod();
                // Sin AllowCredentials(): el token va en el header Authorization,
                // no en cookies, así que no se necesitan credenciales de CORS.
            });
        });
    }

    private static void AddEmail(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<Mail.SmtpSettings>(
            configuration.GetSection("Smtp"));

        services.AddScoped<API.Abstractions.Email.IEmailService,
            Mail.SmtpEmailService>();
    }
    private static void AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
    }

    private static void AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IFacultyRepository, FacultyRepository>();
        services.AddScoped<ICareerRepository, CareerRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISpaceRepository, SpaceRepository>();
        services.AddScoped<IResourceRepository, ResourceRepository>();
        services.AddScoped<IReservationRepository, ReservationRepository>();
        services.AddScoped<IVoucherRepository, VoucherRepository>();
        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<IEmailTemplateRepository, EmailTemplateRepository>();
        services.AddScoped<IEmailLogRepository, EmailLogRepository>();
        services.AddScoped<ReservationService>();
        services.AddScoped<FacultyService>();
        services.AddScoped<CareerService>();
        services.AddScoped<SpaceService>();
        services.AddScoped<ResourceService>();
        services.AddScoped<VoucherService>();
        services.AddScoped<AlertService>();
        services.AddScoped<EmailTemplateService>();
        services.AddScoped<Application.Features.Email.EmailService>();
        services.AddScoped<UserService>();
    }


    private static void AddAuth(this IServiceCollection services, IConfiguration configuration)
    {
        // Esto es para Auditoria - guardado de datos en fecha
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));

        var jwtSettings = configuration.GetSection("Jwt").Get<JwtSettings>();

        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false; // Evita que se mapeen automáticamente los sub de claims
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey))
                };
            });

        services.AddAuthorization();

    }

    private static void AddValidation(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
    }
}