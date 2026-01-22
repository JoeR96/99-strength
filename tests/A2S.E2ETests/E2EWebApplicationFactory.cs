using A2S.Api.Middleware;
using A2S.Api.Services;
using A2S.Application;
using A2S.Application.Common;
using A2S.Domain.Entities;
using A2S.Domain.Repositories;
using A2S.Infrastructure;
using A2S.Infrastructure.Persistence;
using A2S.Tests.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Xunit;

namespace A2S.E2ETests;

/// <summary>
/// WebApplicationFactory for E2E tests that preserves Clerk JWT authentication.
/// Uses Testcontainers for the database but keeps real Clerk authentication working.
/// Starts the server on port 5001 so the frontend can connect to it (matches .env.local config).
/// </summary>
public class E2EWebApplicationFactory : IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;
    private WebApplication? _app;

    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// The base URL where the test API server is running.
    /// Matches the frontend's VITE_API_BASE_URL configuration.
    /// </summary>
    public string ApiBaseUrl => "https://localhost:5001";

    /// <summary>
    /// Initializes the test container and starts the API server.
    /// </summary>
    public async Task InitializeAsync()
    {
        // Start PostgreSQL container
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("a2s_e2e_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithCleanUp(true)
            .Build();

        await _postgresContainer.StartAsync();
        ConnectionString = _postgresContainer.GetConnectionString();
        Console.WriteLine($"PostgreSQL container started. Connection: {ConnectionString}");

        // Start the API server
        await StartApiServerAsync();
    }

    /// <summary>
    /// Starts the API server on port 5001.
    /// </summary>
    private async Task StartApiServerAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });

        // Override connection string to use test database
        builder.Configuration["ConnectionStrings:DefaultConnection"] = ConnectionString;
        builder.Configuration["Serilog:MinimumLevel:Default"] = "Information";

        // Add all the services from the real application
        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);

        // Add controllers from A2S.Api assembly
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(A2S.Api.Controllers.WorkoutsController).Assembly);

        // Configure API Versioning
        builder.Services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
        });

        // Configure Identity
        builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 8;
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<A2SDbContext>()
        .AddDefaultTokenProviders();

        // Configure Clerk JWT Authentication (real Clerk, not mocked)
        var clerkDomain = builder.Configuration["Clerk:Domain"] ?? "https://cosmic-treefrog-30.clerk.accounts.dev";
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.Authority = clerkDomain;
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = clerkDomain,
                NameClaimType = "name",
                RoleClaimType = "role"
            };
            options.RequireHttpsMetadata = false;
        });

        builder.Services.AddAuthorization();

        // Configure CORS
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontend", policy =>
            {
                policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        // Replace DbContext with test connection
        builder.Services.RemoveAll(typeof(DbContextOptions<A2SDbContext>));
        builder.Services.RemoveAll<A2SDbContext>();
        builder.Services.AddDbContext<A2SDbContext>(options =>
        {
            options.UseNpgsql(ConnectionString);
        });

        // Add TestDbContext for identity stores
        builder.Services.AddDbContext<TestDbContext>(options =>
        {
            options.UseNpgsql(ConnectionString);
        });

        // Replace Identity stores to use TestDbContext
        builder.Services.RemoveAll(typeof(IUserStore<ApplicationUser>));
        builder.Services.RemoveAll(typeof(IRoleStore<IdentityRole>));
        builder.Services.AddScoped<IUserStore<ApplicationUser>, Microsoft.AspNetCore.Identity.EntityFrameworkCore.UserStore<ApplicationUser, IdentityRole, TestDbContext>>();
        builder.Services.AddScoped<IRoleStore<IdentityRole>, Microsoft.AspNetCore.Identity.EntityFrameworkCore.RoleStore<IdentityRole, TestDbContext>>();

        // Replace IUserRepository with test implementation
        builder.Services.RemoveAll<IUserRepository>();
        builder.Services.AddScoped<IUserRepository, TestUserRepository>();

        // Add HttpContextAccessor and CurrentUserService for workout user scoping
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

        // Configure URLs
        builder.WebHost.UseUrls(ApiBaseUrl);

        _app = builder.Build();

        // Configure middleware
        _app.UseCors("AllowFrontend");
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.UseAutoProvisionUser();
        _app.MapControllers();

        // Migrate database
        using (var scope = _app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<A2SDbContext>();
            await dbContext.Database.MigrateAsync();

            var testDbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            await testDbContext.Database.EnsureCreatedAsync();
        }

        // Start the server
        await _app.StartAsync();
        Console.WriteLine($"E2E API server started at {ApiBaseUrl}");
    }

    /// <summary>
    /// Deletes all workouts in the database.
    /// Use this to reset the test state before tests that expect no workout.
    /// </summary>
    public async Task DeleteAllWorkoutsAsync()
    {
        if (_app == null) return;

        using var scope = _app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<A2SDbContext>();

        var workouts = await dbContext.Workouts.ToListAsync();

        if (workouts.Any())
        {
            dbContext.Workouts.RemoveRange(workouts);
            await dbContext.SaveChangesAsync();
            Console.WriteLine($"Deleted {workouts.Count} workout(s)");
        }
    }

    /// <summary>
    /// Cleans up the test container and server after tests complete.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (_app != null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }
    }
}
