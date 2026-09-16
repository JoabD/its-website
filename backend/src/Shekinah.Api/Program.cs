using Scalar.AspNetCore;
using Serilog;
using Shekinah.Api.Endpoints;
using Shekinah.Api.Extensions;
using Shekinah.Api.Middleware;
using Shekinah.Application;
using Shekinah.Infrastructure;
using Shekinah.Infrastructure.Persistence;
using Shekinah.Infrastructure.Persistence.Migrations;
using Shekinah.Infrastructure.Seeding;

var builder = WebApplication.CreateBuilder(args);

// --- Serilog: logs estructurados a consola + archivo con correlationId (spec técnico §11) ---
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/shekinah-api-.log", rollingInterval: RollingInterval.Day));

// --- Composition root (spec técnico §4-D) ---
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddShekinahAuth(builder.Configuration);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddOpenApi();

builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy
    // Nota: no se puede usar AllowAnyOrigin() junto con AllowCredentials() (bloqueado por el
    // propio navegador). SetIsOriginAllowed(_ => true) logra el mismo efecto práctico —
    // aceptar cualquier origen— reflejando dinámicamente el Origin recibido, sin romper esa regla.
    // TODO: una vez que los dominios finales (Vercel + custom domain) estén estables, volver a
    // restringir con WithOrigins(...) usando Cors:AllowedOrigins para mayor seguridad.
    .SetIsOriginAllowed(_ => true)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("login", limiterOptions =>
    {
        limiterOptions.PermitLimit = 10;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
    });
    options.AddFixedWindowLimiter("public-forms", limiterOptions =>
    {
        limiterOptions.PermitLimit = 20;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
    });
});

builder.Services.AddHealthChecks()
    .AddMongoDb(name: "mongodb", tags: ["ready"]);

var app = builder.Build();

// --- Migraciones + seed al arrancar (docker compose up levanta el sistema sembrado, DoD §8) ---
using (var scope = app.Services.CreateScope())
{
    var runner = scope.ServiceProvider.GetRequiredService<MigrationRunner>();
    await runner.RunAsync(CancellationToken.None);

    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.SeedAsync(CancellationToken.None);
}

app.UseExceptionHandler();

// Cabeceras de seguridad (spec técnico §11: CSP, HSTS, X-Content-Type-Options, Referrer-Policy).
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'");
    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(); // Scalar UI en /scalar
}

app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapAdmissionsEndpoints();
app.MapCatalogEndpoints();
app.MapAcademicEndpoints();
app.MapStudentEndpoints();
app.MapUsersEndpoints();
app.MapPaymentsEndpoints();
app.MapOperationEndpoints();

app.Run();

// Necesario para que Shekinah.Api.FunctionalTests pueda usar WebApplicationFactory<Program>.
public partial class Program;
