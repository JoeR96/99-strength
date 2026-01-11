# A2S Workout Tracker - Implementation Task List

This document breaks down the strategic plan into explicit step-by-step tasks. Each feature is a complete vertical slice with domain, application, API, frontend, and tests.

---

## Phase 0: Project Foundation

### 0.1 Backend Project Setup

#### Task 0.1.1: Create .NET Solution Structure
- [ ] Create `src/` and `tests/` directories
- [ ] Run `dotnet new sln -n A2S`
- [ ] Run `dotnet new classlib -n A2S.Domain -o src/A2S.Domain`
- [ ] Run `dotnet new classlib -n A2S.Application -o src/A2S.Application`
- [ ] Run `dotnet new classlib -n A2S.Infrastructure -o src/A2S.Infrastructure`
- [ ] Run `dotnet new webapi -n A2S.Api -o src/A2S.Api`
- [ ] Add all projects to solution: `dotnet sln add src/**/*.csproj`
- [ ] Run `dotnet build` to verify
- [ ] **AC**: Solution builds successfully

#### Task 0.1.2: Configure Project References
- [ ] Add reference: `A2S.Application` → `A2S.Domain`
- [ ] Add reference: `A2S.Infrastructure` → `A2S.Application`
- [ ] Add reference: `A2S.Api` → `A2S.Application`
- [ ] Add reference: `A2S.Api` → `A2S.Infrastructure`
- [ ] Run `dotnet build` to verify dependencies
- [ ] **AC**: No circular references, clean build

#### Task 0.1.3: Create Test Projects
- [ ] Run `dotnet new xunit -n A2S.Domain.Tests -o tests/A2S.Domain.Tests`
- [ ] Run `dotnet new xunit -n A2S.Application.Tests -o tests/A2S.Application.Tests`
- [ ] Run `dotnet new xunit -n A2S.Infrastructure.Tests -o tests/A2S.Infrastructure.Tests`
- [ ] Run `dotnet new xunit -n A2S.Api.Tests -o tests/A2S.Api.Tests`
- [ ] Add test projects to solution
- [ ] Add project references (test → corresponding project)
- [ ] Run `dotnet test` to verify
- [ ] **AC**: All test projects run (0 tests)

#### Task 0.1.4: Install Core NuGet Packages - Domain
- [ ] No external dependencies for Domain project
- [ ] **AC**: Domain project has zero external dependencies (clean architecture)

#### Task 0.1.5: Install Core NuGet Packages - Application
- [ ] Install `MediatR` (v12.x)
- [ ] Install `FluentValidation` (v11.x)
- [ ] Run `dotnet build` on A2S.Application
- [ ] **AC**: Application project builds with MediatR and FluentValidation

#### Task 0.1.6: Install Core NuGet Packages - Infrastructure
- [ ] Install `Npgsql.EntityFrameworkCore.PostgreSQL` (v9.x)
- [ ] Install `Microsoft.EntityFrameworkCore.Design` (v9.x)
- [ ] Install `Microsoft.Extensions.Configuration`
- [ ] Run `dotnet build` on A2S.Infrastructure
- [ ] **AC**: Infrastructure project builds

#### Task 0.1.7: Install Core NuGet Packages - API
- [ ] Install `Microsoft.Identity.Web` (for Azure AD)
- [ ] Install `Swashbuckle.AspNetCore` (v6.x)
- [ ] Install `Serilog.AspNetCore`
- [ ] Install `Asp.Versioning.Mvc`
- [ ] Run `dotnet build` on A2S.Api
- [ ] **AC**: API project builds

#### Task 0.1.8: Install Test Packages
- [ ] Install in all test projects: `xunit`, `xunit.runner.visualstudio`
- [ ] Install in all test projects: `FluentAssertions` (v6.x)
- [ ] Install in all test projects: `NSubstitute` (v5.x)
- [ ] Install in Infrastructure.Tests: `Testcontainers` (v3.x)
- [ ] Install in Infrastructure.Tests: `Testcontainers.PostgreSql`
- [ ] Install in Api.Tests: `Microsoft.AspNetCore.Mvc.Testing`
- [ ] Run `dotnet test`
- [ ] **AC**: All test projects build and run

#### Task 0.1.9: Configure EditorConfig
- [ ] Create `.editorconfig` at solution root
- [ ] Configure C# code style (indentation, braces, naming)
- [ ] Set line ending to CRLF (Windows) or LF (Unix)
- [ ] Configure file encoding (UTF-8)
- [ ] **AC**: Code style consistent across solution

#### Task 0.1.10: Create Directory Structure
- [ ] Create `src/A2S.Domain/Entities/`
- [ ] Create `src/A2S.Domain/ValueObjects/`
- [ ] Create `src/A2S.Domain/Services/`
- [ ] Create `src/A2S.Domain/Events/`
- [ ] Create `src/A2S.Application/Commands/`
- [ ] Create `src/A2S.Application/Queries/`
- [ ] Create `src/A2S.Application/DTOs/`
- [ ] Create `src/A2S.Infrastructure/Persistence/`
- [ ] Create `src/A2S.Infrastructure/Repositories/`
- [ ] **AC**: Folder structure follows clean architecture

### 0.2 Database Setup

#### Task 0.2.1: Create Docker Compose for PostgreSQL
- [ ] Create `docker-compose.yml` at solution root
- [ ] Add PostgreSQL service (postgres:16-alpine)
- [ ] Configure environment variables (POSTGRES_USER, POSTGRES_PASSWORD, POSTGRES_DB)
- [ ] Expose port 5432
- [ ] Add volume for data persistence
- [ ] Add pgAdmin service (optional, for dev)
- [ ] Create `.env.example` file with template variables
- [ ] **AC**: `docker-compose up -d` starts PostgreSQL

#### Task 0.2.2: Test Database Connection
- [ ] Run `docker-compose up -d`
- [ ] Use `psql` or pgAdmin to connect to database
- [ ] Create test table and insert row
- [ ] Drop test table
- [ ] **AC**: Can connect and execute SQL

#### Task 0.2.3: Create DbContext Class
- [ ] Create `A2SDbContext.cs` in Infrastructure/Persistence
- [ ] Inherit from `DbContext`
- [ ] Add constructor accepting `DbContextOptions<A2SDbContext>`
- [ ] Override `OnModelCreating` method (empty for now)
- [ ] **AC**: DbContext class compiles

#### Task 0.2.4: Configure Connection String
- [ ] Add connection string to `appsettings.Development.json` in API project
- [ ] Format: `Host=localhost;Database=a2s_dev;Username=postgres;Password=***`
- [ ] Add connection string configuration to `Program.cs`
- [ ] Register DbContext in DI: `builder.Services.AddDbContext<A2SDbContext>`
- [ ] **AC**: DbContext registered in DI container

#### Task 0.2.5: Install EF Core Tools
- [ ] Install `dotnet-ef` globally: `dotnet tool install --global dotnet-ef`
- [ ] Verify installation: `dotnet ef --version`
- [ ] **AC**: EF Core tools available

#### Task 0.2.6: Create Initial Migration
- [ ] Run `dotnet ef migrations add Initial -p src/A2S.Infrastructure -s src/A2S.Api`
- [ ] Review generated migration file (should be empty or minimal)
- [ ] Run `dotnet ef database update -p src/A2S.Infrastructure -s src/A2S.Api`
- [ ] Verify database created in PostgreSQL
- [ ] **AC**: Migration applied, database exists

### 0.3 MediatR and CQRS Setup

#### Task 0.3.1: Register MediatR in DI
- [ ] Open `Program.cs` in A2S.Api
- [ ] Add `builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly))`
- [ ] Add reference to Application assembly for handlers
- [ ] **AC**: MediatR registered

#### Task 0.3.2: Create Base Command and Query Interfaces
- [ ] Create `ICommand.cs` in A2S.Application
- [ ] Create `ICommand<TResponse>.cs` interface inheriting `IRequest<TResponse>`
- [ ] Create `IQuery<TResponse>.cs` interface inheriting `IRequest<TResponse>`
- [ ] **AC**: Base interfaces defined

#### Task 0.3.3: Create Sample Command to Test MediatR
- [ ] Create `PingCommand.cs` implementing `ICommand<string>`
- [ ] Create `PingCommandHandler.cs` implementing `IRequestHandler<PingCommand, string>`
- [ ] Handler returns "Pong"
- [ ] **AC**: Sample command and handler compile

#### Task 0.3.4: Create Test Controller for MediatR
- [ ] Create `TestController.cs` in A2S.Api/Controllers
- [ ] Inject `IMediator`
- [ ] Create GET endpoint `/api/test/ping` that sends `PingCommand`
- [ ] **AC**: Controller compiles

#### Task 0.3.5: Test MediatR End-to-End
- [ ] Run API: `dotnet run --project src/A2S.Api`
- [ ] Call `GET /api/test/ping` via browser or curl
- [ ] Verify response: "Pong"
- [ ] **AC**: MediatR pipeline works

#### Task 0.3.6: Delete Test Code
- [ ] Delete `PingCommand.cs`, `PingCommandHandler.cs`
- [ ] Delete `TestController.cs`
- [ ] **AC**: Test code removed, clean slate

### 0.4 Validation Setup

#### Task 0.4.1: Create Validation Pipeline Behavior
- [ ] Create `ValidationBehavior.cs` in A2S.Application/Behaviors
- [ ] Implement `IPipelineBehavior<TRequest, TResponse>`
- [ ] Inject `IEnumerable<IValidator<TRequest>>`
- [ ] Run all validators before handler execution
- [ ] Throw `ValidationException` if validation fails
- [ ] **AC**: Validation behavior compiles

#### Task 0.4.2: Create ValidationException
- [ ] Create `ValidationException.cs` in A2S.Application/Exceptions
- [ ] Include `IDictionary<string, string[]> Errors` property
- [ ] Constructor accepts FluentValidation `ValidationResult`
- [ ] **AC**: ValidationException defined

#### Task 0.4.3: Register Validation Behavior
- [ ] Register `ValidationBehavior` in MediatR pipeline
- [ ] Update `Program.cs`: `cfg.AddOpenBehavior(typeof(ValidationBehavior<,>))`
- [ ] **AC**: Validation behavior registered

#### Task 0.4.4: Register FluentValidation Validators
- [ ] Add `builder.Services.AddValidatorsFromAssembly(typeof(ApplicationAssemblyMarker).Assembly)`
- [ ] Create `ApplicationAssemblyMarker.cs` empty class for assembly reference
- [ ] **AC**: Validators auto-discovered and registered

### 0.5 Logging Setup

#### Task 0.5.1: Configure Serilog
- [ ] Install `Serilog.Sinks.Console` and `Serilog.Sinks.File` in A2S.Api
- [ ] Configure Serilog in `Program.cs` before `builder.Build()`
- [ ] Set minimum level to Information
- [ ] Add console sink with structured output
- [ ] Add file sink to `logs/app-.log` (rolling daily)
- [ ] **AC**: Serilog configured

#### Task 0.5.2: Add Request Logging
- [ ] Add `app.UseSerilogRequestLogging()` middleware
- [ ] Run API and make test request
- [ ] Check console and log file for request log
- [ ] **AC**: HTTP requests logged

#### Task 0.5.3: Add Correlation ID Middleware
- [ ] Create `CorrelationIdMiddleware.cs` in A2S.Api/Middleware
- [ ] Generate or extract correlation ID from header
- [ ] Add correlation ID to Serilog context
- [ ] Add correlation ID to response headers
- [ ] Register middleware in pipeline
- [ ] **AC**: All logs include correlation ID

### 0.6 API Versioning and Swagger

#### Task 0.6.1: Configure API Versioning
- [ ] Install `Asp.Versioning.Mvc` in A2S.Api
- [ ] Add `builder.Services.AddApiVersioning()` with URL versioning
- [ ] Set default version to 1.0
- [ ] **AC**: API versioning configured

#### Task 0.6.2: Configure Swagger
- [ ] Add `builder.Services.AddSwaggerGen()` in `Program.cs`
- [ ] Configure swagger to support API versions
- [ ] Add XML documentation (enable in .csproj)
- [ ] Add `app.UseSwagger()` and `app.UseSwaggerUI()`
- [ ] **AC**: Swagger UI accessible at `/swagger`

#### Task 0.6.3: Test Swagger
- [ ] Run API
- [ ] Navigate to `https://localhost:5001/swagger`
- [ ] Verify Swagger UI loads
- [ ] **AC**: Swagger UI displays API documentation

### 0.7 Authentication Setup

#### Task 0.7.1: Register Azure AD Application
- [ ] Go to Azure Portal → Azure Active Directory → App Registrations
- [ ] Create new app registration (name: "A2S Workout Tracker")
- [ ] Configure redirect URIs (SPA: `http://localhost:5173`)
- [ ] Note down Application (client) ID
- [ ] Note down Directory (tenant) ID
- [ ] **AC**: Azure AD app registered

#### Task 0.7.2: Configure API Authentication
- [ ] Add Azure AD config to `appsettings.json`:
  ```json
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "YOUR_TENANT_ID",
    "ClientId": "YOUR_CLIENT_ID",
    "Audience": "YOUR_CLIENT_ID"
  }
  ```
- [ ] Add `builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration, "AzureAd")`
- [ ] Add `app.UseAuthentication()` and `app.UseAuthorization()` to pipeline
- [ ] **AC**: Authentication middleware configured

#### Task 0.7.3: Configure Swagger for Authentication
- [ ] Add OAuth2 security definition to Swagger
- [ ] Configure authorization URL and token URL
- [ ] Add security requirement to Swagger UI
- [ ] **AC**: Swagger UI has "Authorize" button

#### Task 0.7.4: Create Protected Test Endpoint
- [ ] Create `AuthTestController.cs`
- [ ] Add `[Authorize]` attribute
- [ ] Create GET endpoint returning user claims
- [ ] **AC**: Endpoint returns 401 without token

#### Task 0.7.5: Test Authentication
- [ ] Use Postman or curl to call protected endpoint without token
- [ ] Verify 401 Unauthorized response
- [ ] **AC**: Authentication enforces authorization

### 0.8 Frontend Project Setup

#### Task 0.8.1: Create Vite React TypeScript Project
- [ ] Run `npm create vite@latest src/A2S.Web -- --template react-ts`
- [ ] `cd src/A2S.Web`
- [ ] Run `npm install`
- [ ] Run `npm run dev`
- [ ] Open browser to `http://localhost:5173`
- [ ] **AC**: Vite dev server runs, React app loads

#### Task 0.8.2: Configure TypeScript
- [ ] Open `tsconfig.json`
- [ ] Enable `"strict": true`
- [ ] Configure path aliases: `"@/*": ["./src/*"]`
- [ ] Install `@types/node` for path resolution
- [ ] Update `vite.config.ts` to support path aliases
- [ ] **AC**: TypeScript strict mode enabled, aliases work

#### Task 0.8.3: Install ESLint and Prettier
- [ ] Install `eslint`, `@typescript-eslint/parser`, `@typescript-eslint/eslint-plugin`
- [ ] Install `prettier`, `eslint-config-prettier`
- [ ] Create `.eslintrc.json` with TypeScript rules
- [ ] Create `.prettierrc` with formatting rules
- [ ] Add scripts to `package.json`: `"lint"`, `"format"`
- [ ] Run `npm run lint`
- [ ] **AC**: Linting works, no errors

#### Task 0.8.4: Create Folder Structure
- [ ] Create `src/components/`
- [ ] Create `src/features/`
- [ ] Create `src/api/`
- [ ] Create `src/hooks/`
- [ ] Create `src/utils/`
- [ ] Create `src/types/`
- [ ] **AC**: Folder structure organized

#### Task 0.8.5: Install Core Dependencies
- [ ] Install `react-router-dom` (v6.x)
- [ ] Install `@tanstack/react-query` (v5.x)
- [ ] Install `axios`
- [ ] Install `@azure/msal-browser` and `@azure/msal-react`
- [ ] **AC**: Core dependencies installed

#### Task 0.8.6: Install UI Dependencies
- [ ] Install `tailwindcss`, `postcss`, `autoprefixer`
- [ ] Run `npx tailwindcss init -p`
- [ ] Configure `tailwind.config.js` with content paths
- [ ] Add Tailwind directives to `src/index.css`
- [ ] Install Radix UI primitives: `@radix-ui/react-dialog`, `@radix-ui/react-select`, etc.
- [ ] Install `react-hot-toast` for notifications
- [ ] **AC**: Tailwind CSS working, UI libraries installed

### 0.9 Frontend Authentication Setup

#### Task 0.9.1: Configure MSAL
- [ ] Create `src/authConfig.ts`
- [ ] Configure MSAL with Azure AD client ID and tenant ID
- [ ] Set redirect URI: `http://localhost:5173`
- [ ] Create MSAL instance
- [ ] **AC**: MSAL configuration file created

#### Task 0.9.2: Create Auth Provider
- [ ] Create `src/components/AuthProvider.tsx`
- [ ] Wrap app with `MsalProvider`
- [ ] Handle authentication state
- [ ] **AC**: AuthProvider component created

#### Task 0.9.3: Create useAuth Hook
- [ ] Create `src/hooks/useAuth.ts`
- [ ] Use `useMsal()` from MSAL React
- [ ] Expose `login()`, `logout()`, `isAuthenticated`, `user`, `getAccessToken()`
- [ ] **AC**: useAuth hook provides auth functionality

#### Task 0.9.4: Create Login Page
- [ ] Create `src/features/auth/LoginPage.tsx`
- [ ] Add "Login with Microsoft" button
- [ ] Call `login()` from useAuth hook
- [ ] **AC**: Login page renders

#### Task 0.9.5: Update App.tsx with Auth
- [ ] Wrap app with `AuthProvider`
- [ ] Show login page if not authenticated
- [ ] Show main app if authenticated
- [ ] **AC**: Auth flow works in UI

#### Task 0.9.6: Test Login Flow
- [ ] Run `npm run dev`
- [ ] Click "Login with Microsoft"
- [ ] Authenticate with Microsoft account
- [ ] Verify redirect back to app
- [ ] **AC**: User can log in via Microsoft Identity

### 0.10 API Client Setup

#### Task 0.10.1: Create Axios Instance
- [ ] Create `src/api/apiClient.ts`
- [ ] Configure base URL: `https://localhost:5001/api/v1`
- [ ] Create axios instance with base config
- [ ] **AC**: API client created

#### Task 0.10.2: Add Request Interceptor for Auth Token
- [ ] Add request interceptor to axios instance
- [ ] Get access token from MSAL
- [ ] Add `Authorization: Bearer {token}` header
- [ ] **AC**: All requests include auth token

#### Task 0.10.3: Add Response Interceptor for Error Handling
- [ ] Add response interceptor
- [ ] Handle 401 (redirect to login)
- [ ] Handle 403 (show error message)
- [ ] Handle 500 (show error toast)
- [ ] **AC**: Errors handled gracefully

#### Task 0.10.4: Create Typed API Client
- [ ] Create `src/api/workouts.ts`
- [ ] Export functions: `getCurrentWorkout()`, `createWorkout()`, etc.
- [ ] Use TypeScript types for request/response
- [ ] **AC**: Typed API client ready for use

### 0.11 React Query Setup

#### Task 0.11.1: Configure React Query
- [ ] Create `src/queryClient.ts`
- [ ] Configure `QueryClient` with default options (staleTime, cacheTime)
- [ ] Export queryClient instance
- [ ] **AC**: QueryClient configured

#### Task 0.11.2: Add QueryClientProvider
- [ ] Update `src/main.tsx`
- [ ] Wrap app with `QueryClientProvider`
- [ ] Add React Query DevTools (`@tanstack/react-query-devtools`)
- [ ] **AC**: React Query provider wraps app

#### Task 0.11.3: Test React Query DevTools
- [ ] Run `npm run dev`
- [ ] Look for React Query DevTools toggle button
- [ ] Open DevTools
- [ ] **AC**: DevTools visible and functional

### 0.12 Testing Infrastructure Setup

#### Task 0.12.1: Configure Testcontainers Fixture
- [ ] Create `tests/A2S.Infrastructure.Tests/DatabaseFixture.cs`
- [ ] Implement `IAsyncLifetime`
- [ ] Spin up PostgreSQL container in `InitializeAsync()`
- [ ] Dispose container in `DisposeAsync()`
- [ ] Expose connection string
- [ ] **AC**: DatabaseFixture compiles

#### Task 0.12.2: Create IntegrationTestBase
- [ ] Create `tests/A2S.Infrastructure.Tests/IntegrationTestBase.cs`
- [ ] Use `IClassFixture<DatabaseFixture>`
- [ ] Create DbContext using test connection string
- [ ] Provide helper methods: `SeedData()`, `GetDbContext()`
- [ ] **AC**: IntegrationTestBase ready for use

#### Task 0.12.3: Test Testcontainers Setup
- [ ] Create sample test: `DatabaseConnectionTest.cs`
- [ ] Test: Connect to database and execute simple query
- [ ] Run `dotnet test`
- [ ] Verify Testcontainer starts and test passes
- [ ] **AC**: Testcontainers working

#### Task 0.12.4: Configure WebApplicationFactory
- [ ] Create `tests/A2S.Api.Tests/CustomWebApplicationFactory.cs`
- [ ] Override `ConfigureWebHost` to use test database
- [ ] Mock authentication (add fake JWT bearer)
- [ ] **AC**: WebApplicationFactory configured

#### Task 0.12.5: Create ApiTestBase
- [ ] Create `tests/A2S.Api.Tests/ApiTestBase.cs`
- [ ] Use `IClassFixture<CustomWebApplicationFactory>`
- [ ] Create authenticated HttpClient
- [ ] Provide helper methods: `GetAsync()`, `PostAsync()`, `AssertStatusCode()`
- [ ] **AC**: ApiTestBase ready for API tests

#### Task 0.12.6: Test API Integration Test Setup
- [ ] Create sample test: `HealthCheckTest.cs`
- [ ] Test: GET /health returns 200 OK
- [ ] Run `dotnet test`
- [ ] **AC**: API integration test passes

#### Task 0.12.7: Install Playwright
- [ ] `cd src/A2S.Web`
- [ ] Run `npm install -D @playwright/test`
- [ ] Run `npx playwright install`
- [ ] **AC**: Playwright installed

#### Task 0.12.8: Configure Playwright
- [ ] Create `playwright.config.ts` in frontend root
- [ ] Set `baseURL: 'http://localhost:5173'`
- [ ] Configure browsers (chromium, firefox, webkit)
- [ ] Set test directory: `tests/e2e`
- [ ] **AC**: Playwright configured

#### Task 0.12.9: Create E2E Test Structure
- [ ] Create `tests/e2e/` folder
- [ ] Create `tests/e2e/fixtures/` for test fixtures
- [ ] Create `tests/e2e/pages/` for page objects
- [ ] **AC**: E2E test structure ready

#### Task 0.12.10: Create Sample E2E Test
- [ ] Create `tests/e2e/sample.spec.ts`
- [ ] Test: Navigate to app, verify title
- [ ] Run `npx playwright test`
- [ ] **AC**: Sample E2E test passes

#### Task 0.12.11: Create Authenticated User Fixture
- [ ] Create `tests/e2e/fixtures/authenticatedUser.ts`
- [ ] Implement fixture that logs in user before tests
- [ ] Store auth state in context
- [ ] **AC**: Authenticated fixture ready

### 0.13 CI/CD Setup

#### Task 0.13.1: Create GitHub Actions Workflow - Build
- [ ] Create `.github/workflows/build.yml`
- [ ] Trigger on push and pull request
- [ ] Steps: Checkout, Setup .NET, Restore, Build
- [ ] **AC**: Build workflow created

#### Task 0.13.2: Create GitHub Actions Workflow - Test
- [ ] Create `.github/workflows/test.yml`
- [ ] Steps: Checkout, Setup .NET, Run `dotnet test`
- [ ] Upload test results as artifact
- [ ] **AC**: Test workflow created

#### Task 0.13.3: Create GitHub Actions Workflow - E2E
- [ ] Create `.github/workflows/e2e.yml`
- [ ] Steps: Checkout, Setup Node, Install deps, Run Playwright
- [ ] Run against staging environment (or docker-compose)
- [ ] **AC**: E2E workflow created

#### Task 0.13.4: Test CI/CD Pipeline
- [ ] Push changes to GitHub
- [ ] Verify all workflows trigger
- [ ] Check that all jobs pass
- [ ] **AC**: CI/CD pipeline works

### 0.14 Git and Version Control

#### Task 0.14.1: Update .gitignore
- [ ] Ensure `bin/`, `obj/`, `node_modules/` ignored
- [ ] Ignore `.vs/`, `.vscode/`, `.idea/`
- [ ] Ignore `*.user`, `*.suo`
- [ ] Ignore `logs/`, `*.log`
- [ ] Ignore `.env`, `.env.local`
- [ ] Ignore `playwright-report/`, `test-results/`
- [ ] **AC**: .gitignore comprehensive

#### Task 0.14.2: Create README.md
- [ ] Add project description
- [ ] Add prerequisites (Node, .NET 9, Docker)
- [ ] Add setup instructions
- [ ] Add how to run (backend, frontend, tests)
- [ ] **AC**: README guides new developers

#### Task 0.14.3: Commit Phase 0 Completion
- [ ] Stage all files: `git add .`
- [ ] Commit: `git commit -m "feat: Complete Phase 0 - Project foundation setup"`
- [ ] **AC**: Phase 0 committed

---

## Phase 1: User Management Feature

### 1.1 User Domain Entity

#### Task 1.1.1: Create User Entity
- [ ] Create `src/A2S.Domain/Entities/User.cs`
- [ ] Add properties: `Guid Id`, `string Email`, `string Name`, `DateTime CreatedAt`
- [ ] Make constructor internal (enforce factory pattern)
- [ ] Add factory method: `User.Create(string email, string name)`
- [ ] **AC**: User entity compiles

#### Task 1.1.2: Add User Validation
- [ ] Email must not be null or empty
- [ ] Email must be valid format (regex)
- [ ] Name must not be null or empty
- [ ] Throw `ArgumentException` for invalid input
- [ ] **AC**: Validation logic in place

#### Task 1.1.3: Write User Entity Unit Tests
- [ ] Create `tests/A2S.Domain.Tests/Entities/UserTests.cs`
- [ ] Test: Valid user creation succeeds
- [ ] Test: Null email throws exception
- [ ] Test: Invalid email format throws exception
- [ ] Test: Null name throws exception
- [ ] Run `dotnet test`
- [ ] **AC**: All user entity tests pass

### 1.2 User Repository

#### Task 1.2.1: Create IUserRepository Interface
- [ ] Create `src/A2S.Domain/Repositories/IUserRepository.cs`
- [ ] Define methods: `Task<User?> GetByIdAsync(Guid id)`, `Task<User?> GetByEmailAsync(string email)`, `Task AddAsync(User user)`, `Task SaveChangesAsync()`
- [ ] **AC**: Repository interface defined

#### Task 1.2.2: Configure User Entity in EF Core
- [ ] Create `src/A2S.Infrastructure/Persistence/Configurations/UserConfiguration.cs`
- [ ] Implement `IEntityTypeConfiguration<User>`
- [ ] Map properties, set primary key
- [ ] Add unique index on Email
- [ ] Set Email max length (256)
- [ ] Set Name max length (100)
- [ ] **AC**: User entity configuration complete

#### Task 1.2.3: Add Users DbSet to DbContext
- [ ] Open `A2SDbContext.cs`
- [ ] Add `public DbSet<User> Users { get; set; }`
- [ ] Apply UserConfiguration in `OnModelCreating`
- [ ] **AC**: DbContext includes Users

#### Task 1.2.4: Create Database Migration for Users
- [ ] Run `dotnet ef migrations add AddUsersTable -p src/A2S.Infrastructure -s src/A2S.Api`
- [ ] Review migration SQL
- [ ] Verify unique index on Email created
- [ ] Run `dotnet ef database update -p src/A2S.Infrastructure -s src/A2S.Api`
- [ ] **AC**: Users table created in database

#### Task 1.2.5: Implement UserRepository
- [ ] Create `src/A2S.Infrastructure/Repositories/UserRepository.cs`
- [ ] Implement `IUserRepository`
- [ ] Inject `A2SDbContext`
- [ ] Implement all methods using EF Core
- [ ] **AC**: UserRepository implementation complete

#### Task 1.2.6: Register UserRepository in DI
- [ ] Open `src/A2S.Api/Program.cs`
- [ ] Add `builder.Services.AddScoped<IUserRepository, UserRepository>()`
- [ ] **AC**: UserRepository registered

#### Task 1.2.7: Write UserRepository Integration Tests
- [ ] Create `tests/A2S.Infrastructure.Tests/Repositories/UserRepositoryTests.cs`
- [ ] Inherit from `IntegrationTestBase`
- [ ] Test: AddAsync persists user to database
- [ ] Test: GetByIdAsync retrieves user
- [ ] Test: GetByIdAsync returns null for non-existent ID
- [ ] Test: GetByEmailAsync retrieves user by email
- [ ] Test: Unique email constraint throws on duplicate
- [ ] Run `dotnet test`
- [ ] **AC**: All UserRepository integration tests pass

### 1.3 User Application Layer

#### Task 1.3.1: Create UserDto
- [ ] Create `src/A2S.Application/DTOs/UserDto.cs`
- [ ] Add properties: `Guid Id`, `string Email`, `string Name`, `DateTime CreatedAt`
- [ ] **AC**: UserDto defined

#### Task 1.3.2: Create CreateUserCommand
- [ ] Create `src/A2S.Application/Commands/Users/CreateUserCommand.cs`
- [ ] Implement `ICommand<UserDto>`
- [ ] Add properties: `string Email`, `string Name`
- [ ] **AC**: CreateUserCommand defined

#### Task 1.3.3: Create CreateUserCommandValidator
- [ ] Create `CreateUserCommandValidator.cs` in same folder
- [ ] Inherit `AbstractValidator<CreateUserCommand>`
- [ ] Validate Email: NotEmpty, EmailAddress
- [ ] Validate Name: NotEmpty, MaxLength(100)
- [ ] **AC**: Validator complete

#### Task 1.3.4: Create CreateUserCommandHandler
- [ ] Create `CreateUserCommandHandler.cs`
- [ ] Implement `IRequestHandler<CreateUserCommand, UserDto>`
- [ ] Inject `IUserRepository`
- [ ] Check if email already exists (throw if duplicate)
- [ ] Create user via factory method
- [ ] Add to repository
- [ ] Save changes
- [ ] Return UserDto
- [ ] **AC**: Handler implementation complete

#### Task 1.3.5: Write CreateUserCommand Application Tests
- [ ] Create `tests/A2S.Application.Tests/Commands/CreateUserCommandHandlerTests.cs`
- [ ] Mock `IUserRepository` with NSubstitute
- [ ] Test: Valid command creates user
- [ ] Test: Duplicate email throws exception
- [ ] Test: Validator rejects invalid email
- [ ] Test: Validator rejects empty name
- [ ] Run `dotnet test`
- [ ] **AC**: All CreateUserCommand tests pass

#### Task 1.3.6: Create GetUserByIdQuery
- [ ] Create `src/A2S.Application/Queries/Users/GetUserByIdQuery.cs`
- [ ] Implement `IQuery<UserDto>`
- [ ] Add property: `Guid UserId`
- [ ] **AC**: Query defined

#### Task 1.3.7: Create GetUserByIdQueryHandler
- [ ] Create `GetUserByIdQueryHandler.cs`
- [ ] Implement `IRequestHandler<GetUserByIdQuery, UserDto>`
- [ ] Inject `IUserRepository`
- [ ] Get user by ID
- [ ] Return UserDto or null
- [ ] **AC**: Handler complete

#### Task 1.3.8: Write GetUserByIdQuery Tests
- [ ] Create `tests/A2S.Application.Tests/Queries/GetUserByIdQueryHandlerTests.cs`
- [ ] Test: Existing user returns UserDto
- [ ] Test: Non-existent user returns null
- [ ] Run `dotnet test`
- [ ] **AC**: Query tests pass

### 1.4 User API Endpoints

#### Task 1.4.1: Create UsersController
- [ ] Create `src/A2S.Api/Controllers/UsersController.cs`
- [ ] Add `[ApiController]`, `[Route("api/v1/users")]`, `[Authorize]`
- [ ] Inject `IMediator`
- [ ] **AC**: Controller created

#### Task 1.4.2: Implement POST /api/v1/users
- [ ] Create `Create` action method
- [ ] Accept `CreateUserCommand` from body
- [ ] Send command via MediatR
- [ ] Return `CreatedAtAction` with UserDto (201)
- [ ] Handle validation exceptions (return 400)
- [ ] **AC**: Endpoint implemented

#### Task 1.4.3: Implement GET /api/v1/users/{id}
- [ ] Create `GetById` action method
- [ ] Accept `Guid id` from route
- [ ] Send `GetUserByIdQuery`
- [ ] Return UserDto (200) or NotFound (404)
- [ ] **AC**: Endpoint implemented

#### Task 1.4.4: Write API Integration Tests for Users
- [ ] Create `tests/A2S.Api.Tests/Controllers/UsersControllerTests.cs`
- [ ] Inherit from `ApiTestBase`
- [ ] Test: POST /users creates user (201)
- [ ] Test: POST /users with invalid email returns 400
- [ ] Test: POST /users with duplicate email returns 400
- [ ] Test: GET /users/{id} returns user (200)
- [ ] Test: GET /users/{id} with non-existent ID returns 404
- [ ] Run `dotnet test`
- [ ] **AC**: All API tests pass

### 1.5 Auto-Create User on Login

#### Task 1.5.1: Create GetOrCreateUserFromClaimsQuery
- [ ] Create `src/A2S.Application/Queries/Users/GetOrCreateUserFromClaimsQuery.cs`
- [ ] Add properties: `string Email`, `string Name`
- [ ] Implement `IQuery<UserDto>`
- [ ] **AC**: Query defined

#### Task 1.5.2: Create GetOrCreateUserFromClaimsQueryHandler
- [ ] Create handler
- [ ] Try to get user by email
- [ ] If not found, create new user
- [ ] Return UserDto
- [ ] **AC**: Handler creates user on first login

#### Task 1.5.3: Create Middleware to Auto-Create User
- [ ] Create `src/A2S.Api/Middleware/AutoProvisionUserMiddleware.cs`
- [ ] Extract email and name from authenticated user claims
- [ ] Send `GetOrCreateUserFromClaimsQuery`
- [ ] Store UserId in HttpContext.Items
- [ ] **AC**: Middleware auto-provisions user

#### Task 1.5.4: Register Middleware
- [ ] Add middleware to pipeline in `Program.cs` (after UseAuthentication)
- [ ] **AC**: Middleware registered

#### Task 1.5.5: Test Auto-Provision Flow
- [ ] Create API integration test
- [ ] Authenticate with fake user claims
- [ ] Call any protected endpoint
- [ ] Verify user auto-created in database
- [ ] **AC**: Auto-provision works

### 1.6 User Management E2E Test

#### Task 1.6.1: Create UserManagement Page Object
- [ ] Create `tests/e2e/pages/UserPage.ts`
- [ ] Add methods: `navigate()`, `getUserName()`
- [ ] **AC**: Page object created

#### Task 1.6.2: Write E2E Test - User Login Creates User
- [ ] Create `tests/e2e/user.spec.ts`
- [ ] Test: User logs in for first time
- [ ] Verify user name displayed in UI
- [ ] **AC**: E2E test passes

#### Task 1.6.3: Commit User Feature
- [ ] Run all tests: `dotnet test` and `npx playwright test`
- [ ] Commit: `git commit -m "feat: User management feature complete"`
- [ ] **AC**: User feature committed

---

## Phase 2: Workout Creation Feature

### 2.1 Domain - Value Objects

#### Task 2.1.1: Create TrainingMax Value Object
- [ ] Create `src/A2S.Domain/ValueObjects/TrainingMax.cs`
- [ ] Add properties: `decimal Weight`, `WeightUnit Unit` (enum: Kg, Lbs)
- [ ] Make it a `record` for value equality
- [ ] Add validation: Weight > 0
- [ ] Add method: `decimal CalculatePercentage(decimal percentage)`
- [ ] **AC**: TrainingMax value object complete

#### Task 2.1.2: Write TrainingMax Unit Tests
- [ ] Create `tests/A2S.Domain.Tests/ValueObjects/TrainingMaxTests.cs`
- [ ] Test: Valid TrainingMax creation
- [ ] Test: Zero weight throws exception
- [ ] Test: Negative weight throws exception
- [ ] Test: CalculatePercentage returns correct value
- [ ] Test: Value equality (two TMs with same values are equal)
- [ ] Run `dotnet test`
- [ ] **AC**: All TrainingMax tests pass

#### Task 2.1.3: Create RepRange Value Object
- [ ] Create `src/A2S.Domain/ValueObjects/RepRange.cs`
- [ ] Add properties: `int MinReps`, `int TargetReps`, `int MaxReps`
- [ ] Make it a `record`
- [ ] Add validation: MinReps < TargetReps < MaxReps, all > 0
- [ ] Add method: `bool Contains(int reps)`
- [ ] Add static factory methods: `RepRange.For8To10To12()`, etc.
- [ ] **AC**: RepRange value object complete

#### Task 2.1.4: Write RepRange Unit Tests
- [ ] Create `tests/A2S.Domain.Tests/ValueObjects/RepRangeTests.cs`
- [ ] Test: Valid RepRange creation
- [ ] Test: Invalid constraints throw exception
- [ ] Test: Contains() method works correctly
- [ ] Test: Factory methods return correct ranges
- [ ] Run `dotnet test`
- [ ] **AC**: All RepRange tests pass

#### Task 2.1.5: Create PlannedSet Value Object
- [ ] Create `src/A2S.Domain/ValueObjects/PlannedSet.cs`
- [ ] Add properties: `int SetNumber`, `int TargetReps`, `decimal Weight`, `WeightUnit Unit`, `bool IsAMRAP`
- [ ] Make it a `record`
- [ ] Add display method: `string ToDisplayString()` (e.g., "100kg × 6")
- [ ] **AC**: PlannedSet value object complete

#### Task 2.1.6: Write PlannedSet Unit Tests
- [ ] Create `tests/A2S.Domain.Tests/ValueObjects/PlannedSetTests.cs`
- [ ] Test: PlannedSet creation
- [ ] Test: ToDisplayString() formats correctly
- [ ] Test: IsAMRAP flag works
- [ ] Run `dotnet test`
- [ ] **AC**: PlannedSet tests pass

### 2.2 Domain - Exercise Progression Base

#### Task 2.2.1: Create Equipment Enum
- [ ] Create `src/A2S.Domain/Enums/EquipmentType.cs`
- [ ] Values: `Barbell`, `Dumbbell`, `Cable`, `Machine`, `Bodyweight`, `SmithMachine`
- [ ] **AC**: Enum defined

#### Task 2.2.2: Create ExerciseProgression Abstract Base Class
- [ ] Create `src/A2S.Domain/Entities/ExerciseProgression.cs`
- [ ] Make abstract class
- [ ] Add properties: `Guid Id`, `ProgressionType Type` (enum)
- [ ] Add abstract method: `IReadOnlyList<PlannedSet> CalculatePlannedSets(int weekNumber)`
- [ ] Add abstract method: `void ProcessActivity(WorkoutActivity activity)`
- [ ] **AC**: Base class defined

#### Task 2.2.3: Create LinearProgressionStrategy Class
- [ ] Create `src/A2S.Domain/Entities/LinearProgressionStrategy.cs`
- [ ] Inherit from `ExerciseProgression`
- [ ] Add properties: `TrainingMax CurrentTM`, `Dictionary<int, decimal> RepBonusTable`
- [ ] Implement `CalculatePlannedSets(int weekNumber)` (placeholder for now)
- [ ] Implement `ProcessActivity(WorkoutActivity activity)` (placeholder for now)
- [ ] **AC**: LinearProgressionStrategy class skeleton complete

#### Task 2.2.4: Create RepsPerSetStrategy Class
- [ ] Create `src/A2S.Domain/Entities/RepsPerSetStrategy.cs`
- [ ] Inherit from `ExerciseProgression`
- [ ] Add properties: `RepRange RepRange`, `int CurrentSets`, `int TargetSets`, `int StartingSets`, `decimal CurrentWeight`, `WeightUnit Unit`, `EquipmentType Equipment`
- [ ] Implement `CalculatePlannedSets(int weekNumber)` (placeholder)
- [ ] Implement `ProcessActivity(WorkoutActivity activity)` (placeholder)
- [ ] **AC**: RepsPerSetStrategy class skeleton complete

### 2.3 Domain - Workout Aggregate (Part 1)

#### Task 2.3.1: Create WorkoutActivity Value Object
- [ ] Create `src/A2S.Domain/ValueObjects/WorkoutActivity.cs`
- [ ] Make it a `record`
- [ ] Add properties: `DateTime Date`, `DayOfWeek DayOfWeek`, `List<ExerciseActivity> ExerciseActivities`
- [ ] Add validation: Date not in future
- [ ] **AC**: WorkoutActivity value object complete

#### Task 2.3.2: Create ExerciseActivity Value Object
- [ ] Create `src/A2S.Domain/ValueObjects/ExerciseActivity.cs`
- [ ] Make it a `record`
- [ ] Add properties: `Guid ExerciseId`, `List<SetActivity> Sets`
- [ ] **AC**: ExerciseActivity defined

#### Task 2.3.3: Create SetActivity Value Object
- [ ] Create `src/A2S.Domain/ValueObjects/SetActivity.cs`
- [ ] Make it a `record`
- [ ] Add properties: `int SetNumber`, `int ActualReps`, `decimal Weight`, `WeightUnit Unit`
- [ ] **AC**: SetActivity defined

#### Task 2.3.4: Create Exercise Entity
- [ ] Create `src/A2S.Domain/Entities/Exercise.cs`
- [ ] Add properties: `Guid Id`, `string Name`, `ExerciseType Type` (enum), `int OrderIndex`, `Guid WorkoutId`
- [ ] Add navigation property: `ExerciseProgression Progression`
- [ ] Add collection: `List<WorkoutActivity> Activities`
- [ ] Add method: `void RecordActivity(WorkoutActivity activity)`
- [ ] **AC**: Exercise entity defined

#### Task 2.3.5: Create Workout Entity (Aggregate Root)
- [ ] Create `src/A2S.Domain/Entities/Workout.cs`
- [ ] Add properties: `Guid Id`, `Guid UserId`, `DateTime StartDate`, `int CurrentWeek`, `DateTime CreatedAt`
- [ ] Add collection: `List<Exercise> Exercises`
- [ ] Add factory method: `Workout.Create(Guid userId, DateTime startDate)`
- [ ] Add method: `Exercise AddExercise(string name, ExerciseType type, ExerciseProgression progression)`
- [ ] **AC**: Workout aggregate root defined

#### Task 2.3.6: Write Workout Unit Tests (Part 1)
- [ ] Create `tests/A2S.Domain.Tests/Entities/WorkoutTests.cs`
- [ ] Test: Valid workout creation
- [ ] Test: Start date in past is allowed
- [ ] Test: AddExercise adds exercise to collection
- [ ] Test: Cannot add exercise with duplicate name
- [ ] Run `dotnet test`
- [ ] **AC**: Workout basic tests pass

### 2.4 Infrastructure - Workout Persistence

#### Task 2.4.1: Configure Exercise Entity Mapping
- [ ] Create `src/A2S.Infrastructure/Persistence/Configurations/ExerciseConfiguration.cs`
- [ ] Implement `IEntityTypeConfiguration<Exercise>`
- [ ] Map properties
- [ ] Configure relationship to Workout (required, cascade delete)
- [ ] Configure owned entity for WorkoutActivities (as JSON column)
- [ ] Add unique constraint (WorkoutId + Name)
- [ ] **AC**: Exercise configuration complete

#### Task 2.4.2: Configure ExerciseProgression Mapping (TPH)
- [ ] Create `src/A2S.Infrastructure/Persistence/Configurations/ExerciseProgressionConfiguration.cs`
- [ ] Configure TPH with discriminator column "ProgressionType"
- [ ] Map LinearProgressionStrategy properties
- [ ] Map RepsPerSetStrategy properties
- [ ] Configure owned entity for TrainingMax
- [ ] Configure owned entity for RepRange (RepsPerSet only)
- [ ] **AC**: ExerciseProgression TPH mapping complete

#### Task 2.4.3: Configure Workout Entity Mapping
- [ ] Create `src/A2S.Infrastructure/Persistence/Configurations/WorkoutConfiguration.cs`
- [ ] Map properties
- [ ] Add index on UserId
- [ ] Configure relationship to User
- [ ] Configure relationship to Exercises collection
- [ ] Add RowVersion for optimistic concurrency
- [ ] **AC**: Workout configuration complete

#### Task 2.4.4: Add DbSets to DbContext
- [ ] Add `DbSet<Workout> Workouts`
- [ ] Add `DbSet<Exercise> Exercises`
- [ ] Add `DbSet<ExerciseProgression> ExerciseProgressions`
- [ ] Apply configurations in `OnModelCreating`
- [ ] **AC**: DbContext includes workout entities

#### Task 2.4.5: Create Database Migration for Workouts
- [ ] Run `dotnet ef migrations add AddWorkoutAggregate`
- [ ] Review migration SQL
- [ ] Verify TPH discriminator column created
- [ ] Verify JSON column for WorkoutActivities
- [ ] Run `dotnet ef database update`
- [ ] **AC**: Workout tables created

### 2.5 Infrastructure - Workout Repository

#### Task 2.5.1: Create IWorkoutRepository Interface
- [ ] Create `src/A2S.Domain/Repositories/IWorkoutRepository.cs`
- [ ] Define methods:
  - `Task<Workout?> GetByIdAsync(Guid id)`
  - `Task<Workout?> GetCurrentByUserIdAsync(Guid userId)`
  - `Task AddAsync(Workout workout)`
  - `Task SaveChangesAsync()`
- [ ] **AC**: Repository interface defined

#### Task 2.5.2: Implement WorkoutRepository
- [ ] Create `src/A2S.Infrastructure/Repositories/WorkoutRepository.cs`
- [ ] Implement all interface methods
- [ ] Use `.Include(w => w.Exercises).ThenInclude(e => e.Progression)` for eager loading
- [ ] Filter by UserId in `GetCurrentByUserIdAsync` (security)
- [ ] **AC**: WorkoutRepository implemented

#### Task 2.5.3: Register WorkoutRepository in DI
- [ ] Add `builder.Services.AddScoped<IWorkoutRepository, WorkoutRepository>()` to `Program.cs`
- [ ] **AC**: WorkoutRepository registered

#### Task 2.5.4: Write WorkoutRepository Integration Tests
- [ ] Create `tests/A2S.Infrastructure.Tests/Repositories/WorkoutRepositoryTests.cs`
- [ ] Test: AddAsync persists workout with exercises
- [ ] Test: GetByIdAsync retrieves workout with eager loading
- [ ] Test: GetCurrentByUserIdAsync returns most recent workout
- [ ] Test: Polymorphic ExerciseProgression persisted correctly
- [ ] Test: WorkoutActivities JSON serialization works
- [ ] Run `dotnet test`
- [ ] **AC**: All WorkoutRepository tests pass

### 2.6 Application - Create Workout Command

#### Task 2.6.1: Create WorkoutDto
- [ ] Create `src/A2S.Application/DTOs/WorkoutDto.cs`
- [ ] Add properties: `Guid Id`, `Guid UserId`, `DateTime StartDate`, `int CurrentWeek`, `List<ExerciseDto> Exercises`
- [ ] **AC**: WorkoutDto defined

#### Task 2.6.2: Create ExerciseDto
- [ ] Create `src/A2S.Application/DTOs/ExerciseDto.cs`
- [ ] Add properties: `Guid Id`, `string Name`, `ExerciseType Type`, `ExerciseProgressionDto Progression`
- [ ] **AC**: ExerciseDto defined

#### Task 2.6.3: Create ExerciseProgressionDto (Base)
- [ ] Create `src/A2S.Application/DTOs/ExerciseProgressionDto.cs`
- [ ] Add discriminator property: `ProgressionType Type`
- [ ] **AC**: Base DTO defined

#### Task 2.6.4: Create LinearProgressionDto
- [ ] Create `src/A2S.Application/DTOs/LinearProgressionDto.cs`
- [ ] Inherit from `ExerciseProgressionDto`
- [ ] Add property: `TrainingMaxDto CurrentTM`
- [ ] **AC**: LinearProgressionDto defined

#### Task 2.6.5: Create RepsPerSetProgressionDto
- [ ] Create `src/A2S.Application/DTOs/RepsPerSetProgressionDto.cs`
- [ ] Inherit from `ExerciseProgressionDto`
- [ ] Add properties: `RepRangeDto RepRange`, `int CurrentSets`, `decimal CurrentWeight`
- [ ] **AC**: RepsPerSetProgressionDto defined

#### Task 2.6.6: Create CreateWorkoutCommand
- [ ] Create `src/A2S.Application/Commands/Workouts/CreateWorkoutCommand.cs`
- [ ] Implement `ICommand<WorkoutDto>`
- [ ] Add properties:
  - `Guid UserId`
  - `DateTime StartDate`
  - `List<CreateExerciseDto> Exercises`
- [ ] **AC**: Command defined

#### Task 2.6.7: Create CreateExerciseDto
- [ ] Create DTO with: `string Name`, `ExerciseType Type`, `CreateProgressionDto Progression`
- [ ] **AC**: DTO defined

#### Task 2.6.8: Create CreateProgressionDto (Discriminated Union)
- [ ] Create base DTO with discriminator
- [ ] Create `CreateLinearProgressionDto` (with initial TM)
- [ ] Create `CreateRepsPerSetProgressionDto` (with rep range, starting sets, weight)
- [ ] **AC**: Progression DTOs defined

#### Task 2.6.9: Create CreateWorkoutCommandValidator
- [ ] Validate: UserId not empty
- [ ] Validate: StartDate not in future
- [ ] Validate: Exercises collection not empty
- [ ] Validate: Each exercise has valid name
- [ ] Validate: Main lifts (Squat, Bench, Deadlift, OHP) present
- [ ] **AC**: Validator complete

#### Task 2.6.10: Create CreateWorkoutCommandHandler
- [ ] Inject `IWorkoutRepository`, `IUserRepository`
- [ ] Verify user exists
- [ ] Check no active workout exists for user
- [ ] Create Workout aggregate
- [ ] For each exercise, create progression strategy
- [ ] Add exercises to workout
- [ ] Persist via repository
- [ ] Return WorkoutDto
- [ ] **AC**: Handler implementation complete

#### Task 2.6.11: Write CreateWorkoutCommand Application Tests
- [ ] Create `tests/A2S.Application.Tests/Commands/CreateWorkoutCommandHandlerTests.cs`
- [ ] Test: Valid command creates workout
- [ ] Test: Main lifts missing throws validation error
- [ ] Test: User not found throws exception
- [ ] Test: Active workout already exists throws exception
- [ ] Run `dotnet test`
- [ ] **AC**: All CreateWorkout tests pass

### 2.7 Application - Get Current Workout Query

#### Task 2.7.1: Create GetCurrentWorkoutQuery
- [ ] Create `src/A2S.Application/Queries/Workouts/GetCurrentWorkoutQuery.cs`
- [ ] Implement `IQuery<WorkoutDto?>`
- [ ] Add property: `Guid UserId`
- [ ] **AC**: Query defined

#### Task 2.7.2: Create GetCurrentWorkoutQueryHandler
- [ ] Inject `IWorkoutRepository`
- [ ] Call `GetCurrentByUserIdAsync`
- [ ] Map to WorkoutDto
- [ ] Return null if not found
- [ ] **AC**: Handler complete

#### Task 2.7.3: Configure AutoMapper for Workout DTOs
- [ ] Create `src/A2S.Application/Mappings/WorkoutMappingProfile.cs`
- [ ] Inherit `Profile`
- [ ] Map: `Workout` → `WorkoutDto`
- [ ] Map: `Exercise` → `ExerciseDto`
- [ ] Map: `LinearProgressionStrategy` → `LinearProgressionDto`
- [ ] Map: `RepsPerSetStrategy` → `RepsPerSetProgressionDto`
- [ ] Register AutoMapper in `Program.cs`
- [ ] **AC**: AutoMapper configured

#### Task 2.7.4: Write GetCurrentWorkoutQuery Tests
- [ ] Test: Returns WorkoutDto when workout exists
- [ ] Test: Returns null when no workout
- [ ] Run `dotnet test`
- [ ] **AC**: Query tests pass

### 2.8 API - Workout Endpoints

#### Task 2.8.1: Create WorkoutsController
- [ ] Create `src/A2S.Api/Controllers/WorkoutsController.cs`
- [ ] Add attributes, inject IMediator
- [ ] **AC**: Controller created

#### Task 2.8.2: Implement POST /api/v1/workouts
- [ ] Create `Create` action
- [ ] Extract UserId from claims (or HttpContext.Items)
- [ ] Send `CreateWorkoutCommand`
- [ ] Return 201 Created with WorkoutDto
- [ ] Handle validation errors (400)
- [ ] **AC**: Endpoint implemented

#### Task 2.8.3: Implement GET /api/v1/workouts/current
- [ ] Create `GetCurrent` action
- [ ] Extract UserId from claims
- [ ] Send `GetCurrentWorkoutQuery`
- [ ] Return 200 OK with WorkoutDto or 404 Not Found
- [ ] **AC**: Endpoint implemented

#### Task 2.8.4: Write Workouts API Integration Tests
- [ ] Create `tests/A2S.Api.Tests/Controllers/WorkoutsControllerTests.cs`
- [ ] Test: POST /workouts creates workout (201)
- [ ] Test: POST /workouts with missing main lifts returns 400
- [ ] Test: POST /workouts when workout exists returns 400
- [ ] Test: GET /workouts/current returns workout (200)
- [ ] Test: GET /workouts/current returns 404 when no workout
- [ ] Run `dotnet test`
- [ ] **AC**: All API tests pass

### 2.9 Frontend - Workout Creation Wizard

#### Task 2.9.1: Create Workout Types
- [ ] Create `src/types/workout.ts`
- [ ] Define types: `Workout`, `Exercise`, `ExerciseProgression`, `TrainingMax`, `RepRange`
- [ ] Match backend DTOs
- [ ] **AC**: Types defined

#### Task 2.9.2: Create API Client Functions
- [ ] Create `src/api/workouts.ts`
- [ ] Export `createWorkout(data: CreateWorkoutRequest): Promise<Workout>`
- [ ] Export `getCurrentWorkout(): Promise<Workout | null>`
- [ ] **AC**: API functions defined

#### Task 2.9.3: Create useCreateWorkout Mutation Hook
- [ ] Create `src/hooks/useCreateWorkout.ts`
- [ ] Use `useMutation` from React Query
- [ ] Call `api.workouts.createWorkout()`
- [ ] Invalidate `currentWorkout` query on success
- [ ] **AC**: Hook created

#### Task 2.9.4: Create useCurrentWorkout Query Hook
- [ ] Create `src/hooks/useCurrentWorkout.ts`
- [ ] Use `useQuery` from React Query
- [ ] Call `api.workouts.getCurrentWorkout()`
- [ ] **AC**: Hook created

#### Task 2.9.5: Create SetupWizard Component
- [ ] Create `src/features/workout/SetupWizard.tsx`
- [ ] Use multi-step form (3 steps)
- [ ] Step 1: Welcome and explanation
- [ ] Step 2: Training max input
- [ ] Step 3: Exercise selection
- [ ] Add navigation buttons (Next, Back, Create)
- [ ] **AC**: Wizard component renders

#### Task 2.9.6: Create TrainingMaxInput Component
- [ ] Create `src/features/workout/TrainingMaxInput.tsx`
- [ ] Form with inputs for Squat, Bench, Deadlift, OHP
- [ ] Weight unit selector (kg/lbs)
- [ ] Validation (positive numbers)
- [ ] Helper text explaining TM concept
- [ ] **AC**: Component validates and displays TMs

#### Task 2.9.7: Create ExerciseSelection Component
- [ ] Create `src/features/workout/ExerciseSelection.tsx`
- [ ] Display main lifts (pre-selected, disabled)
- [ ] Display auxiliary exercises (checkboxes)
- [ ] Allow user to select accessories
- [ ] **AC**: User can select exercises

#### Task 2.9.8: Wire Wizard to API
- [ ] In SetupWizard, collect form data
- [ ] On final step submit, call `useCreateWorkout.mutate()`
- [ ] Navigate to dashboard on success
- [ ] Show error toast on failure
- [ ] **AC**: Wizard creates workout via API

#### Task 2.9.9: Create Route for Wizard
- [ ] Update `src/App.tsx` router
- [ ] Add route: `/setup` → `SetupWizard`
- [ ] **AC**: Route accessible

### 2.10 Frontend - Workout Dashboard

#### Task 2.10.1: Create WorkoutDashboard Component
- [ ] Create `src/features/workout/WorkoutDashboard.tsx`
- [ ] Use `useCurrentWorkout` hook
- [ ] Display loading state while fetching
- [ ] Show "No workout" message if null
- [ ] Display current week number and block
- [ ] **AC**: Dashboard shows current workout

#### Task 2.10.2: Create WeekOverview Component
- [ ] Create `src/features/workout/WeekOverview.tsx`
- [ ] Display training days for current week
- [ ] Show exercises scheduled for each day
- [ ] Indicate completed vs pending days (placeholder for now)
- [ ] **AC**: Week overview renders

#### Task 2.10.3: Create Route for Dashboard
- [ ] Add route: `/dashboard` → `WorkoutDashboard`
- [ ] Make it default authenticated route
- [ ] **AC**: Dashboard accessible

#### Task 2.10.4: Redirect Logic
- [ ] In App.tsx, check if user has workout
- [ ] If no workout, redirect to `/setup`
- [ ] If workout exists, redirect to `/dashboard`
- [ ] **AC**: User redirected appropriately

### 2.11 E2E Test - Workout Creation Flow

#### Task 2.11.1: Create Workout Page Objects
- [ ] Create `tests/e2e/pages/SetupWizardPage.ts`
- [ ] Add methods: `navigate()`, `enterTrainingMaxes()`, `selectExercises()`, `submit()`
- [ ] Create `tests/e2e/pages/DashboardPage.ts`
- [ ] Add methods: `navigate()`, `getCurrentWeek()`, `isVisible()`
- [ ] **AC**: Page objects created

#### Task 2.11.2: Write E2E Test - Create Workout
- [ ] Create `tests/e2e/workout-creation.spec.ts`
- [ ] Test: User completes setup wizard
- [ ] Verify workout created
- [ ] Verify redirected to dashboard
- [ ] Verify dashboard shows week 1
- [ ] Run `npx playwright test`
- [ ] **AC**: E2E test passes

#### Task 2.11.3: Commit Workout Creation Feature
- [ ] Run all tests
- [ ] Commit: `git commit -m "feat: Workout creation feature complete"`
- [ ] **AC**: Feature committed

---

## Phase 3: Workout Execution Feature

### 3.1 Domain - PlannedSet Calculation

#### Task 3.1.1: Create PlannedSetCalculator Domain Service
- [ ] Create `src/A2S.Domain/Services/PlannedSetCalculator.cs`
- [ ] Add method: `IReadOnlyList<PlannedSet> CalculateSetsForWeek(Exercise exercise, int weekNumber)`
- [ ] Delegate to exercise.Progression.CalculatePlannedSets(weekNumber)
- [ ] Apply deload logic for weeks 7, 14, 21 (reduce volume)
- [ ] Round weights to nearest increment (2.5kg or 5lbs)
- [ ] **AC**: Calculator service defined

#### Task 3.1.2: Implement LinearProgressionStrategy.CalculatePlannedSets()
- [ ] Determine rep target based on week and block
- [ ] Calculate intensity percentage (increases across weeks)
- [ ] Calculate weight: `TM * intensity%`
- [ ] Generate 4 standard sets + 1 AMRAP set
- [ ] Return list of PlannedSets
- [ ] **AC**: LinearProgression calculates sets correctly

#### Task 3.1.3: Write LinearProgressionStrategy Tests
- [ ] Test: Week 1 calculates correct sets (high reps, lower intensity)
- [ ] Test: Week 15 calculates correct sets (low reps, higher intensity)
- [ ] Test: Deload week (week 7) reduces volume
- [ ] Test: AMRAP set is final set
- [ ] Run `dotnet test`
- [ ] **AC**: LinearProgression tests pass

#### Task 3.1.4: Implement RepsPerSetStrategy.CalculatePlannedSets()
- [ ] Return CurrentSets × TargetReps at CurrentWeight
- [ ] All sets have same reps (no AMRAP)
- [ ] **AC**: RepsPerSet calculates sets correctly

#### Task 3.1.5: Write RepsPerSetStrategy Tests
- [ ] Test: CurrentSets=3 generates 3 sets
- [ ] Test: TargetReps=10 sets reps to 10 for all sets
- [ ] Test: CurrentWeight used for all sets
- [ ] Run `dotnet test`
- [ ] **AC**: RepsPerSet tests pass

### 3.2 Application - Get Planned Sets Query

#### Task 3.2.1: Create PlannedSetDto
- [ ] Create `src/A2S.Application/DTOs/PlannedSetDto.cs`
- [ ] Add properties: `int SetNumber`, `int TargetReps`, `decimal Weight`, `string WeightUnit`, `bool IsAMRAP`
- [ ] **AC**: DTO defined

#### Task 3.2.2: Create GetPlannedSetsQuery
- [ ] Create `src/A2S.Application/Queries/Workouts/GetPlannedSetsQuery.cs`
- [ ] Implement `IQuery<IReadOnlyList<PlannedSetDto>>`
- [ ] Add properties: `Guid WorkoutId`, `Guid ExerciseId`, `int WeekNumber`
- [ ] **AC**: Query defined

#### Task 3.2.3: Create GetPlannedSetsQueryHandler
- [ ] Inject `IWorkoutRepository`, `PlannedSetCalculator`
- [ ] Load workout
- [ ] Find exercise by ID
- [ ] Call calculator service
- [ ] Map to PlannedSetDto
- [ ] Return list
- [ ] **AC**: Handler complete

#### Task 3.2.4: Write GetPlannedSetsQuery Tests
- [ ] Test: Returns planned sets for exercise
- [ ] Test: Uses correct week number
- [ ] Run `dotnet test`
- [ ] **AC**: Query tests pass

### 3.3 Application - Complete Day Command

#### Task 3.3.1: Create CompleteDayCommand
- [ ] Create `src/A2S.Application/Commands/Workouts/CompleteDayCommand.cs`
- [ ] Implement `ICommand<WorkoutDto>`
- [ ] Add properties:
  - `Guid WorkoutId`
  - `Guid UserId`
  - `DateTime Date`
  - `List<ExerciseActivityDto> ExerciseActivities`
- [ ] **AC**: Command defined

#### Task 3.3.2: Create ExerciseActivityDto
- [ ] Add properties: `Guid ExerciseId`, `List<SetActivityDto> Sets`
- [ ] **AC**: DTO defined

#### Task 3.3.3: Create SetActivityDto
- [ ] Add properties: `int SetNumber`, `int ActualReps`, `decimal Weight`, `string WeightUnit`
- [ ] **AC**: DTO defined

#### Task 3.3.4: Create CompleteDayCommandValidator
- [ ] Validate: WorkoutId not empty
- [ ] Validate: Date not in future
- [ ] Validate: ExerciseActivities not empty
- [ ] Validate: Each exercise has sets
- [ ] Validate: AMRAP set has actual reps > 0
- [ ] **AC**: Validator complete

#### Task 3.3.5: Implement Exercise.RecordActivity()
- [ ] Map ExerciseActivityDto to WorkoutActivity value object
- [ ] Add activity to Activities collection
- [ ] Call Progression.ProcessActivity(activity)
- [ ] **AC**: Domain method records activity

#### Task 3.3.6: Implement Workout.CompleteDay()
- [ ] Add method to Workout aggregate
- [ ] Accept date and list of exercise activities
- [ ] For each exercise, call RecordActivity()
- [ ] Raise DayCompleted domain event
- [ ] **AC**: Domain method completes day

#### Task 3.3.7: Create CompleteDayCommandHandler
- [ ] Inject `IWorkoutRepository`
- [ ] Load workout
- [ ] Verify user owns workout
- [ ] Call `workout.CompleteDay()`
- [ ] Save changes
- [ ] Return updated WorkoutDto
- [ ] **AC**: Handler implementation complete

#### Task 3.3.8: Write CompleteDayCommand Tests
- [ ] Test: Valid command completes day
- [ ] Test: User doesn't own workout throws exception
- [ ] Test: Date in future throws validation error
- [ ] Run `dotnet test`
- [ ] **AC**: CompleteDay tests pass

### 3.4 Domain - Progression Processing

#### Task 3.4.1: Implement LinearProgressionStrategy.ProcessActivity()
- [ ] Extract AMRAP set from activity
- [ ] Get actual reps from AMRAP set
- [ ] Calculate target reps for the week
- [ ] Calculate rep delta (actual - target)
- [ ] Use rep bonus table to get adjustment %
- [ ] Apply adjustment to CurrentTM
- [ ] Clamp adjustment to max ±10%
- [ ] Round new TM to increment
- [ ] **AC**: Linear progression processes activity

#### Task 3.4.2: Write LinearProgressionStrategy.ProcessActivity() Tests
- [ ] Test: Beat target by 3 reps → TM increases 1.5%
- [ ] Test: Hit target exactly → TM unchanged
- [ ] Test: Missed target by 2 reps → TM decreases 5%
- [ ] Test: Adjustment clamped at 10%
- [ ] Run `dotnet test`
- [ ] **AC**: ProcessActivity tests pass

#### Task 3.4.3: Implement RepsPerSetStrategy.ProcessActivity()
- [ ] Evaluate performance:
  - SUCCESS: All sets hit MaxReps
  - MAINTAINED: All sets hit at least MinReps
  - FAILED: Any set below MinReps
- [ ] Apply progression logic:
  - SUCCESS: Add set OR increase weight
  - FAILED: Remove set OR decrease weight
  - MAINTAINED: No change
- [ ] Use equipment-based weight increment
- [ ] **AC**: RepsPerSet progression processes activity

#### Task 3.4.4: Write RepsPerSetStrategy.ProcessActivity() Tests
- [ ] Test: All sets hit max → CurrentSets increases
- [ ] Test: CurrentSets at target and all max → weight increases
- [ ] Test: Any set fails → CurrentSets decreases
- [ ] Test: Dumbbell uses 1kg increment under 10kg
- [ ] Test: Dumbbell uses 2kg increment over 10kg
- [ ] Run `dotnet test`
- [ ] **AC**: RepsPerSet ProcessActivity tests pass

### 3.5 API - Workout Activity Endpoints

#### Task 3.5.1: Implement POST /api/v1/workouts/{id}/complete-day
- [ ] Create action in WorkoutsController
- [ ] Extract UserId from claims
- [ ] Map request body to CompleteDayCommand
- [ ] Send command
- [ ] Return 200 OK with updated WorkoutDto
- [ ] Handle concurrency conflicts (409)
- [ ] **AC**: Endpoint implemented

#### Task 3.5.2: Implement GET /api/v1/workouts/{id}/planned-sets
- [ ] Create action in WorkoutsController
- [ ] Accept query params: `exerciseId`, `weekNumber`
- [ ] Send GetPlannedSetsQuery
- [ ] Return 200 OK with PlannedSetDto[]
- [ ] **AC**: Endpoint implemented

#### Task 3.5.3: Write API Tests for Activity Endpoints
- [ ] Test: POST /complete-day completes day successfully
- [ ] Test: POST /complete-day with invalid data returns 400
- [ ] Test: GET /planned-sets returns correct sets
- [ ] Run `dotnet test`
- [ ] **AC**: API tests pass

### 3.6 Frontend - Planned Workout View

#### Task 3.6.1: Create usePlannedSets Hook
- [ ] Create `src/hooks/usePlannedSets.ts`
- [ ] Accept `workoutId`, `exerciseId`, `weekNumber`
- [ ] Use `useQuery` to fetch planned sets
- [ ] **AC**: Hook created

#### Task 3.6.2: Create PlannedWorkout Component
- [ ] Create `src/features/workout/PlannedWorkout.tsx`
- [ ] Use `useCurrentWorkout` to get workout
- [ ] For each exercise, use `usePlannedSets` to get sets
- [ ] Display exercises and planned sets in cards
- [ ] Add "Start Workout" button
- [ ] **AC**: Planned workout displays

#### Task 3.6.3: Create ExerciseCard Component
- [ ] Create `src/features/workout/ExerciseCard.tsx`
- [ ] Display exercise name
- [ ] Show planned sets in table format
- [ ] Highlight AMRAP set
- [ ] **AC**: Exercise card renders

#### Task 3.6.4: Create Route for Planned Workout
- [ ] Add route: `/workout/day/:dayOfWeek` → `PlannedWorkout`
- [ ] **AC**: Route accessible

### 3.7 Frontend - Workout Logger

#### Task 3.7.1: Create useCompleteDay Hook
- [ ] Create `src/hooks/useCompleteDay.ts`
- [ ] Use `useMutation`
- [ ] Call `api.workouts.completeDay()`
- [ ] Invalidate `currentWorkout` query on success
- [ ] **AC**: Hook created

#### Task 3.7.2: Create WorkoutLogger Component
- [ ] Create `src/features/workout/WorkoutLogger.tsx`
- [ ] Display exercises for the day
- [ ] For each exercise, show sets with input fields for actual reps
- [ ] Track completion state (all sets logged)
- [ ] Add "Complete Workout" button (enabled when all logged)
- [ ] **AC**: Logger component renders

#### Task 3.7.3: Create SetLogger Component
- [ ] Create `src/features/workout/SetLogger.tsx`
- [ ] Display set number, planned weight, planned reps
- [ ] Input field for actual reps
- [ ] Visual feedback when set completed (checkmark)
- [ ] Auto-focus next set on completion
- [ ] **AC**: Set logger UX smooth

#### Task 3.7.4: Wire Logger to API
- [ ] Collect all exercise activities
- [ ] On "Complete Workout" click, call `useCompleteDay.mutate()`
- [ ] Navigate to summary on success
- [ ] Show error toast on failure
- [ ] **AC**: Logger saves workout

#### Task 3.7.5: Add Workout Timer
- [ ] Add timer component showing elapsed time
- [ ] Start timer when workout begins
- [ ] Optional rest timer between sets
- [ ] **AC**: Timer runs during workout

### 3.8 Frontend - Post-Workout Summary

#### Task 3.8.1: Create WorkoutSummary Component
- [ ] Create `src/features/workout/WorkoutSummary.tsx`
- [ ] Accept completed workout data as prop
- [ ] Display exercises completed
- [ ] Show rep performance (actual vs planned)
- [ ] Highlight TM changes
- [ ] Button to return to dashboard
- [ ] **AC**: Summary displays

#### Task 3.8.2: Create ProgressionFeedback Component
- [ ] Create `src/features/workout/ProgressionFeedback.tsx`
- [ ] Display TM changes with visual indicators (+/- %)
- [ ] Show rep delta for each exercise
- [ ] For RepsPerSet: show set progression
- [ ] Add motivational messaging
- [ ] **AC**: Feedback component shows progression

#### Task 3.8.3: Navigate to Summary After Complete
- [ ] After `useCompleteDay` succeeds, navigate to `/workout/summary`
- [ ] Pass workout data via state or refetch
- [ ] **AC**: User sees summary after workout

### 3.9 E2E Test - Workout Execution Flow

#### Task 3.9.1: Create Workout Execution Page Objects
- [ ] Create `tests/e2e/pages/PlannedWorkoutPage.ts`
- [ ] Create `tests/e2e/pages/WorkoutLoggerPage.ts`
- [ ] Create `tests/e2e/pages/WorkoutSummaryPage.ts`
- [ ] Add methods for interactions
- [ ] **AC**: Page objects created

#### Task 3.9.2: Write E2E Test - Complete Workout
- [ ] Create `tests/e2e/workout-execution.spec.ts`
- [ ] Test: User views planned workout
- [ ] Test: User logs all sets
- [ ] Test: User completes workout
- [ ] Test: Summary shows TM increase
- [ ] Run `npx playwright test`
- [ ] **AC**: E2E test passes

#### Task 3.9.3: Commit Workout Execution Feature
- [ ] Run all tests
- [ ] Commit: `git commit -m "feat: Workout execution feature complete"`
- [ ] **AC**: Feature committed

---

## Phase 4: Week Progression Feature

### 4.1 Domain - Week Progression

#### Task 4.1.1: Add Workout.ProgressToNextWeek() Method
- [ ] Validate current week is complete (check activities)
- [ ] Increment CurrentWeek
- [ ] Raise WeekProgressed domain event
- [ ] **AC**: Domain method progresses week

#### Task 4.1.2: Write Workout.ProgressToNextWeek() Tests
- [ ] Test: Valid progression increments week
- [ ] Test: Cannot progress with incomplete days
- [ ] Test: Week 21 marks program complete
- [ ] Run `dotnet test`
- [ ] **AC**: Week progression tests pass

### 4.2 Application - Progress Week Command

#### Task 4.2.1: Create ProgressToNextWeekCommand
- [ ] Create command with `Guid WorkoutId`, `Guid UserId`
- [ ] Implement `ICommand<WorkoutDto>`
- [ ] **AC**: Command defined

#### Task 4.2.2: Create ProgressToNextWeekCommandValidator
- [ ] Validate: WorkoutId not empty
- [ ] Validate: User owns workout
- [ ] **AC**: Validator complete

#### Task 4.2.3: Create ProgressToNextWeekCommandHandler
- [ ] Load workout
- [ ] Verify user ownership
- [ ] Call `workout.ProgressToNextWeek()`
- [ ] Save changes
- [ ] Return updated WorkoutDto
- [ ] **AC**: Handler complete

#### Task 4.2.4: Write ProgressToNextWeek Tests
- [ ] Test: Valid command progresses week
- [ ] Test: Incomplete week throws exception
- [ ] Run `dotnet test`
- [ ] **AC**: Command tests pass

### 4.3 API - Week Progression Endpoint

#### Task 4.3.1: Implement POST /api/v1/workouts/{id}/progress
- [ ] Create action in WorkoutsController
- [ ] Send ProgressToNextWeekCommand
- [ ] Return 200 OK with updated WorkoutDto
- [ ] Handle validation errors (400)
- [ ] **AC**: Endpoint implemented

#### Task 4.3.2: Write API Test for Progression
- [ ] Test: POST /progress succeeds when week complete
- [ ] Test: POST /progress returns 400 for incomplete week
- [ ] Run `dotnet test`
- [ ] **AC**: API test passes

### 4.4 Frontend - Week Progression

#### Task 4.4.1: Create useProgressWeek Hook
- [ ] Create `src/hooks/useProgressWeek.ts`
- [ ] Use `useMutation`
- [ ] Call `api.workouts.progressWeek()`
- [ ] Invalidate queries on success
- [ ] **AC**: Hook created

#### Task 4.4.2: Create WeekProgressionModal Component
- [ ] Create `src/features/workout/WeekProgressionModal.tsx`
- [ ] Show when week is complete
- [ ] Display week summary (days completed, exercises done)
- [ ] Show next week preview (intensity increase)
- [ ] Button to progress to next week
- [ ] **AC**: Modal displays

#### Task 4.4.3: Trigger Modal on Week Completion
- [ ] In WorkoutDashboard, check if week is complete
- [ ] Show modal if complete and not yet progressed
- [ ] Call `useProgressWeek.mutate()` on button click
- [ ] **AC**: Modal triggers automatically

### 4.5 E2E Test - Week Progression

#### Task 4.5.1: Write E2E Test - Progress Week
- [ ] Create `tests/e2e/week-progression.spec.ts`
- [ ] Test: Complete all days in week 1
- [ ] Test: Progression modal appears
- [ ] Test: Click "Progress to Week 2"
- [ ] Test: Dashboard shows week 2
- [ ] Run `npx playwright test`
- [ ] **AC**: E2E test passes

#### Task 4.5.2: Commit Week Progression Feature
- [ ] Run all tests
- [ ] Commit: `git commit -m "feat: Week progression feature complete"`
- [ ] **AC**: Feature committed

---

## Phase 5: Exercise History & Progression Charts

### 5.1 Application - Exercise Progression Query

#### Task 5.1.1: Create ExerciseProgressionDto
- [ ] Create DTO with progression data over time
- [ ] Include: week number, TM value, volume, intensity
- [ ] **AC**: DTO defined

#### Task 5.1.2: Create GetExerciseProgressionQuery
- [ ] Add properties: `Guid ExerciseId`, `int StartWeek`, `int EndWeek`
- [ ] Implement `IQuery<List<ExerciseProgressionDto>>`
- [ ] **AC**: Query defined

#### Task 5.1.3: Create GetExerciseProgressionQueryHandler
- [ ] Load exercise with activities
- [ ] Calculate progression data for each week
- [ ] Map to ExerciseProgressionDto
- [ ] **AC**: Handler complete

#### Task 5.1.4: Write GetExerciseProgression Tests
- [ ] Test: Returns progression timeline
- [ ] Test: Calculates volume correctly
- [ ] Run `dotnet test`
- [ ] **AC**: Query tests pass

### 5.2 Application - Workout History Query

#### Task 5.2.1: Create WorkoutHistoryDto
- [ ] Add properties: summary of completed workouts
- [ ] **AC**: DTO defined

#### Task 5.2.2: Create GetWorkoutHistoryQuery
- [ ] Add properties: `Guid UserId`, pagination params
- [ ] Implement `IQuery<PaginatedList<WorkoutHistoryDto>>`
- [ ] **AC**: Query defined

#### Task 5.2.3: Create GetWorkoutHistoryQueryHandler
- [ ] Load workouts for user
- [ ] Apply pagination
- [ ] Map to WorkoutHistoryDto
- [ ] **AC**: Handler complete

#### Task 5.2.4: Write GetWorkoutHistory Tests
- [ ] Test: Returns paginated history
- [ ] Run `dotnet test`
- [ ] **AC**: Query tests pass

### 5.3 API - History Endpoints

#### Task 5.3.1: Implement GET /api/v1/exercises/{id}/progression
- [ ] Accept query params: startWeek, endWeek
- [ ] Send GetExerciseProgressionQuery
- [ ] Return progression timeline
- [ ] **AC**: Endpoint implemented

#### Task 5.3.2: Implement GET /api/v1/workouts/history
- [ ] Accept pagination query params
- [ ] Send GetWorkoutHistoryQuery
- [ ] Return paginated history
- [ ] **AC**: Endpoint implemented

#### Task 5.3.3: Write API Tests for History
- [ ] Test: GET /progression returns timeline
- [ ] Test: GET /history returns paginated results
- [ ] Run `dotnet test`
- [ ] **AC**: API tests pass

### 5.4 Frontend - Exercise Progression Charts

#### Task 5.4.1: Install Charting Library
- [ ] Install `recharts` or `chart.js` with React wrapper
- [ ] **AC**: Charting library installed

#### Task 5.4.2: Create useExerciseProgression Hook
- [ ] Fetch progression data for exercise
- [ ] **AC**: Hook created

#### Task 5.4.3: Create ExerciseProgressionChart Component
- [ ] Display TM over time (line chart)
- [ ] Display volume over time (bar chart)
- [ ] Show week markers and block transitions
- [ ] **AC**: Chart visualizes progression

#### Task 5.4.4: Create ExerciseHistory Page
- [ ] List all exercises in workout
- [ ] Click exercise to view progression chart
- [ ] Show statistics (best week, total volume, avg intensity)
- [ ] **AC**: User can view exercise history

#### Task 5.4.5: Create Route for Exercise History
- [ ] Add route: `/history/exercises/:exerciseId`
- [ ] **AC**: Route accessible

### 5.5 Frontend - Workout History

#### Task 5.5.1: Create useWorkoutHistory Hook
- [ ] Use `useInfiniteQuery` for pagination
- [ ] **AC**: Hook created

#### Task 5.5.2: Create WorkoutHistory Component
- [ ] Display list of past workouts
- [ ] Show date, week number, exercises completed
- [ ] Click workout to view details
- [ ] Implement infinite scroll
- [ ] **AC**: History browsable

#### Task 5.5.3: Create Route for Workout History
- [ ] Add route: `/history`
- [ ] **AC**: Route accessible

### 5.6 E2E Test - History and Charts

#### Task 5.6.1: Write E2E Test - View Progression
- [ ] Complete multiple weeks
- [ ] Navigate to exercise history
- [ ] Verify chart displays
- [ ] Run `npx playwright test`
- [ ] **AC**: E2E test passes

#### Task 5.6.2: Commit History Feature
- [ ] Run all tests
- [ ] Commit: `git commit -m "feat: Exercise history and progression charts complete"`
- [ ] **AC**: Feature committed

---

## Phase 6: Polish & Production Readiness

### 6.1 Error Handling

#### Task 6.1.1: Create Global Error Boundary
- [ ] Create `src/components/ErrorBoundary.tsx`
- [ ] Catch React errors
- [ ] Display friendly error page
- [ ] Provide recovery action (reload)
- [ ] **AC**: Errors don't crash app

#### Task 6.1.2: Add API Error Handling
- [ ] Handle network errors (retry with backoff)
- [ ] Handle 401/403 (redirect to login)
- [ ] Handle 409 conflict (show merge UI or force reload)
- [ ] Handle 500 (show error message)
- [ ] **AC**: API errors handled gracefully

#### Task 6.1.3: Add Offline Support Detection
- [ ] Detect when user goes offline
- [ ] Show offline indicator
- [ ] Queue mutations for when online
- [ ] **AC**: App handles offline state

### 6.2 Performance Optimization

#### Task 6.2.1: Implement Code Splitting
- [ ] Lazy load routes with `React.lazy`
- [ ] Add loading fallbacks
- [ ] **AC**: Initial bundle size reduced

#### Task 6.2.2: Optimize API Queries
- [ ] Add database indexes for common queries
- [ ] Implement response caching headers
- [ ] Use pagination for large datasets
- [ ] **AC**: API response times < 200ms

#### Task 6.2.3: Add Loading Skeletons
- [ ] Create skeleton components for all data views
- [ ] Show skeletons during loading states
- [ ] **AC**: No blank screens during load

### 6.3 Accessibility

#### Task 6.3.1: Add ARIA Labels and Roles
- [ ] Label all form inputs
- [ ] Add roles to interactive elements
- [ ] Ensure focus management in modals
- [ ] **AC**: Screen reader can navigate app

#### Task 6.3.2: Implement Keyboard Navigation
- [ ] Ensure all actions keyboard accessible
- [ ] Add keyboard shortcuts for common actions
- [ ] Test tab order
- [ ] **AC**: App fully keyboard navigable

#### Task 6.3.3: Add Focus Indicators
- [ ] Ensure visible focus states
- [ ] Test high contrast mode
- [ ] **AC**: Focus always visible

### 6.4 Testing & Quality

#### Task 6.4.1: Achieve 80%+ Test Coverage
- [ ] Run test coverage report
- [ ] Write tests for uncovered code
- [ ] **AC**: 80%+ coverage on domain and application layers

#### Task 6.4.2: Performance Testing
- [ ] Load test API endpoints (simulate 100 concurrent users)
- [ ] Identify bottlenecks
- [ ] Optimize slow queries
- [ ] **AC**: API handles load without degradation

### 6.5 Deployment

#### Task 6.5.1: Create Production Docker Images
- [ ] Multi-stage Dockerfile for API
- [ ] Nginx Dockerfile for frontend
- [ ] Docker compose for production stack
- [ ] **AC**: App runs in containers

#### Task 6.5.2: Configure Production Environment
- [ ] Set up PostgreSQL database (managed service)
- [ ] Configure Azure AD app registration for production
- [ ] Set environment variables
- [ ] Configure SSL/TLS
- [ ] **AC**: Production environment ready

#### Task 6.5.3: Deploy to Production
- [ ] Deploy via GitHub Actions
- [ ] Push to container registry
- [ ] Deploy to hosting platform (Azure, AWS, etc.)
- [ ] Verify deployment successful
- [ ] **AC**: App live in production

### 6.6 Documentation

#### Task 6.6.1: Complete API Documentation
- [ ] Ensure all endpoints documented in Swagger
- [ ] Add usage examples
- [ ] Document authentication flow
- [ ] **AC**: API fully documented

#### Task 6.6.2: Write User Guide
- [ ] Document how to use app
- [ ] Explain A2S methodology
- [ ] Add troubleshooting section
- [ ] **AC**: Users can self-serve help

#### Task 6.6.3: Write Developer Documentation
- [ ] Document architecture decisions
- [ ] Add setup instructions
- [ ] Document testing strategy
- [ ] **AC**: New developers can onboard

#### Task 6.6.4: Final Commit
- [ ] Commit: `git commit -m "chore: Production ready - v1.0.0"`
- [ ] Tag release: `git tag v1.0.0`
- [ ] **AC**: Version 1.0 released

---

## Success Criteria Summary

### Phase 0 Complete:
- [ ] Solution structure created
- [ ] Database running in Docker
- [ ] Authentication working
- [ ] All testing infrastructure functional
- [ ] Frontend and backend communicating

### Phase 1 Complete:
- [ ] Users can be created
- [ ] Users auto-provisioned on login
- [ ] All tests passing

### Phase 2 Complete:
- [ ] Users can create workouts via wizard
- [ ] Workouts persisted to database
- [ ] Dashboard displays current workout
- [ ] E2E test covers workflow

### Phase 3 Complete:
- [ ] Users can view planned workouts
- [ ] Users can log sets and complete workouts
- [ ] Progression algorithms update TM/sets
- [ ] Post-workout summary displays
- [ ] E2E test covers execution flow

### Phase 4 Complete:
- [ ] Users can progress to next week
- [ ] Week completion validated
- [ ] E2E test covers week progression

### Phase 5 Complete:
- [ ] Exercise progression charts display
- [ ] Workout history browsable
- [ ] E2E test covers history views

### Phase 6 Complete:
- [ ] Error handling robust
- [ ] Performance optimized
- [ ] Accessibility standards met
- [ ] App deployed to production
- [ ] Documentation complete

---

## Estimated Effort

- **Phase 0**: ~40 hours (setup heavy)
- **Phase 1**: ~12 hours
- **Phase 2**: ~20 hours
- **Phase 3**: ~24 hours
- **Phase 4**: ~8 hours
- **Phase 5**: ~16 hours
- **Phase 6**: ~20 hours

**Total**: ~140 hours to production-ready v1.0
