using A2S.Api.Authentication;
using A2S.Api.Infrastructure;
using A2S.Api.Middleware;
using A2S.Api.Services;
using A2S.Application;
using A2S.Application.Common;
using A2S.Domain.Aggregates.User;
using A2S.Domain.Common;
using A2S.Infrastructure.Security;
using A2S.Infrastructure;
using A2S.Infrastructure.Persistence;
using A2S.Infrastructure.SeedData;
using A2S.Integration.Hevy;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Logging hygiene: EF command logging (which can echo parameters) is kept at Warning, and the
// User aggregate is destructured without its protected key. ReadFrom.Services lets tests plug
// in a capturing sink to prove no secret ever reaches a log event.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    .Destructure.ByTransforming<User>(u => new { Id = u.Id.Value, u.Email, u.Name, u.HasHevyApiKey })
    .ReadFrom.Services(services)
    .WriteTo.Console()
    .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHevyIntegration(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddHttpClient();
builder.Services.AddHttpClient("HevyApi");

builder.Services.AddControllers(options =>
{
    options.Conventions.Add(new NonProductionActionsConvention(builder.Environment.IsProduction()));
});

var apiVersioning = builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});

// Only add API Explorer in Development (causes OpenAPI conflicts in tests)
if (builder.Environment.IsDevelopment())
{
    apiVersioning.AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });
}

// Both have empty placeholders in appsettings.json; an unset or empty value stops the boot here.
var clerkDomain = RequiredSettings.ClerkDomain(builder.Configuration);
var corsOrigins = RequiredSettings.CorsOrigins(builder.Configuration);

// Clerk session JWTs for the web app, personal API tokens (nns_pat_…) for the desktop widget.
builder.Services.AddA2SAuthentication(clerkDomain, requireHttpsMetadata: !builder.Environment.IsDevelopment());

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Configure Swagger (only in Development to avoid assembly conflicts in tests)
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
}

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<A2SDbContext>();

    // Verify database is up to date — migrations are applied as a deploy step
    // (EF migration bundle), not at app startup. This avoids multi-instance
    // races, slow/failed boots, and the need for the app identity to hold DDL rights.
    var pending = (await dbContext.Database.GetPendingMigrationsAsync()).ToList();
    if (pending.Count > 0)
    {
        throw new InvalidOperationException(
            $"Database has {pending.Count} pending migration(s): {string.Join(", ", pending)}. " +
            "Run the EF migration bundle via the deploy pipeline before starting the API.");
    }

    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await ExerciseDefinitionSeeder.SeedAsync(dbContext, logger);

    // Encrypt any pre-v2 plaintext Hevy keys. Idempotent; logs a count only.
    var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
    await HevyApiKeyBackfill.RunAsync(dbContext, protector, logger);

    // Tag the exercise library with Hevy primary/secondary muscle groups for the volume budget. Idempotent.
    await MuscleGroupBackfill.RunAsync(dbContext, logger);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "A2S API V1");
    });
}

app.UseHttpsRedirection();

app.UseCors("AllowFrontend");

// Global exception handler — must be early in the pipeline
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

app.UseMiddleware<CorrelationIdMiddleware>();

app.UseSerilogRequestLogging();

app.UseAuthentication();
// A personal API token only reaches endpoints marked [AllowApiToken]; anything else is 403.
app.UseApiTokenScope();
app.UseAuthorization();

// Auto-provision user on first authenticated request
app.UseAutoProvisionUser();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

// Make Program class accessible for WebApplicationFactory in tests
public partial class Program { }
