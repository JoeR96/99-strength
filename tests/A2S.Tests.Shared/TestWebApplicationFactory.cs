using A2S.Domain.Entities;
using A2S.Domain.Repositories;
using A2S.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Testcontainers.PostgreSql;
using Xunit;

namespace A2S.Tests.Shared;

/// <summary>
/// Base WebApplicationFactory for integration and E2E tests.
/// Handles test database setup using Testcontainers.
/// Uses a simplified TestDbContext for authentication tests.
/// </summary>
public class TestWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram>, IAsyncLifetime
    where TProgram : class
{
    private PostgreSqlContainer? _postgresContainer;

    public string ConnectionString { get; private set; } = string.Empty;

    // JWT settings shared between configuration and test helpers
    public const string TestJwtSecret = "ThisIsATestSecretKeyForJwtTokenGenerationThatIs64CharactersLongForHS256";
    public const string TestJwtIssuer = "A2STestIssuer";
    public const string TestJwtAudience = "A2STestAudience";

    /// <summary>
    /// Initializes the test container before tests run.
    /// </summary>
    public async Task InitializeAsync()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("a2s_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithCleanUp(true)
            .Build();


        await _postgresContainer.StartAsync();
        ConnectionString = _postgresContainer.GetConnectionString();
    }

    /// <summary>
    /// Cleans up the test container after tests complete.
    /// </summary>
    public new async Task DisposeAsync()
    {
        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Provide test configuration
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = TestJwtSecret,
                ["Jwt:Issuer"] = TestJwtIssuer,
                ["Jwt:Audience"] = TestJwtAudience,
                ["ConnectionStrings:DefaultConnection"] = ConnectionString,
                ["Serilog:MinimumLevel:Default"] = "Warning" // Reduce logging noise in tests
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Remove the existing DbContext configurations
            services.RemoveAll(typeof(DbContextOptions<A2SDbContext>));
            services.RemoveAll<A2SDbContext>();

            // Add simplified TestDbContext for auth tests only
            // This avoids EF Core model validation issues with complex Workout entities
            services.AddDbContext<TestDbContext>(options =>
            {
                options.UseNpgsql(ConnectionString);
            });

            // Replace the Identity EF Core stores to use TestDbContext instead of A2SDbContext
            // Remove only the EF Core store services
            services.RemoveAll(typeof(IUserStore<ApplicationUser>));
            services.RemoveAll(typeof(IRoleStore<IdentityRole>));

            // Add the stores back with TestDbContext
            services.AddScoped<IUserStore<ApplicationUser>, Microsoft.AspNetCore.Identity.EntityFrameworkCore.UserStore<ApplicationUser, IdentityRole, TestDbContext>>();
            services.AddScoped<IRoleStore<IdentityRole>, Microsoft.AspNetCore.Identity.EntityFrameworkCore.RoleStore<IdentityRole, TestDbContext>>();

            // Replace IUserRepository with a test implementation that uses TestDbContext
            services.RemoveAll<IUserRepository>();
            services.AddScoped<IUserRepository, TestUserRepository>();

            // Reconfigure JWT Bearer options with test secret
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = TestJwtIssuer,
                    ValidAudience = TestJwtAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtSecret))
                };
            });

            // Ensure test database is created
            var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            dbContext.Database.EnsureCreated();
        });
    }
}
