using Microsoft.OpenApi.Models;
using QuestPDF.Infrastructure;
using SpaceReservationSystem.API.Middlewares;
using SpaceReservationSystem.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration); // Agrega la infraestructura y la base de datos al contenedor de servicios

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configuracion de Swagger
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme // la API usa autenticación tipo Bearer
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingrese el token JWT. Ejemplo: Bearer {token}"
    });

    // Swagger aplica el esquema a los endpoints, para que una vez que pegues el token en "Authorize"
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseCors("AngularClient");
app.UseAuthentication(); // Habilita la autenticación
app.UseAuthorization(); // Habilita la autorización
app.MapControllers();
app.Run();