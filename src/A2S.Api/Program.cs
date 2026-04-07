using A2S.Api.Middleware;
using A2S.Api.Services;
using A2S.Application;
using A2S.Application.Common;
using A2S.Infrastructure;
using A2S.Infrastructure.Persistence;
using A2S.Infrastructure.SeedData;
using A2S.Integration.Hevy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHevyIntegration(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddHttpClient();
builder.Services.AddHttpClient("HevyApi");

builder.Services.AddControllers();

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

var clerkDomain = builder.Configuration["Clerk:Domain"]
    ?? throw new InvalidOperationException("Clerk:Domain configuration is required. Set 'Clerk:Domain' in appsettings or environment variables.");

var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
    ?? throw new InvalidOperationException("Cors:Origins configuration is required. Set 'Cors:Origins' in appsettings or environment variables.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Authority = clerkDomain;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = clerkDomain,
        NameClaimType = "name",
        RoleClaimType = "role"
    };
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
});

builder.Services.AddAuthorization();

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
    await dbContext.Database.MigrateAsync();

    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await ExerciseDefinitionSeeder.SeedAsync(dbContext, logger);
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
app.UseAuthorization();

// Auto-provision user on first authenticated request
app.UseAutoProvisionUser();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

// Make Program class accessible for WebApplicationFactory in tests
public partial class Program { }
