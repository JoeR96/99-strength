# Running the A2S Workout Tracker

This guide shows you how to run the full stack application (database, backend API, and frontend).

## Prerequisites

- **Docker Desktop** (for PostgreSQL database)
- **.NET 9 SDK** (for backend API)
- **Node.js 18+** (for frontend)
- **npm** (comes with Node.js)

## Quick Start - Run Everything at Once

From the **root directory** (`99-strength`):

```bash
# Start database, API, and frontend all at once
npm start
```

This will:
1. Start PostgreSQL in Docker
2. Start the .NET API on https://localhost:5001
3. Start the Vite dev server on http://localhost:5173

**To stop the database when done**:
```bash
npm run stop:db
```

## Running Components Individually

### 1. Start the Database Only

```bash
# Start PostgreSQL
npm run start:db

# Or use docker-compose directly
docker-compose up -d
```

**Check database is running**:
```bash
docker ps
```

You should see `a2s_postgres` container running.

**pgAdmin** is also available at http://localhost:5050:
- Email: `admin@a2s.local`
- Password: `admin`

### 2. Start the Backend API

```bash
# Option 1: Using npm script
npm run start:api

# Option 2: From the API directory
cd src/A2S.Api
dotnet run
```

The API will be available at:
- HTTPS: https://localhost:5001
- HTTP: http://localhost:5000
- Swagger: https://localhost:5001/swagger

### 3. Start the Frontend

```bash
# Option 1: Using npm script
npm run start:web

# Option 2: From the frontend directory
cd src/A2S.Web
npm run dev
```

The frontend will be available at: http://localhost:5173

## Running Tests

### E2E Tests (Playwright)

```bash
# Run all E2E tests (headless)
npm run test:e2e

# Run tests and SEE the browser (RECOMMENDED for watching tests)
npm run test:e2e:headed

# Run tests in UI mode (interactive)
npm run test:e2e:ui

# Debug a specific test
npm run test:e2e:debug
```

### Backend Tests (.NET)

```bash
npm run test:backend

# Or directly
dotnet test
```

## Building for Production

```bash
# Build both API and frontend
npm run build

# Build individually
npm run build:api
npm run build:web
```

## Initial Setup (First Time Only)

If this is your first time running the project:

```bash
# Install all dependencies and set up database
npm run setup
```

This will:
1. Start PostgreSQL in Docker
2. Restore .NET packages and build API
3. Install npm packages for frontend

## Troubleshooting

### Database not starting

**Problem**: `docker-compose up -d` fails

**Solutions**:
1. Make sure Docker Desktop is running
2. Check port 5432 is not in use: `netstat -ano | findstr :5432`
3. Try: `docker-compose down` then `docker-compose up -d`

### API not starting

**Problem**: `dotnet run` fails

**Solutions**:
1. Ensure database is running first
2. Check connection string in `src/A2S.Api/appsettings.Development.json`
3. Try: `dotnet restore` then `dotnet build`

### Frontend not loading Clerk

**Problem**: Frontend shows errors about missing Clerk key

**Solutions**:
1. Ensure `.env.local` exists in `src/A2S.Web/`
2. Verify `VITE_CLERK_PUBLISHABLE_KEY` is set
3. Restart the dev server: `Ctrl+C` then `npm run dev`

### Playwright tests failing

**Problem**: Tests timeout or fail

**Solutions**:
1. Make sure frontend dev server is running: `npm run start:web`
2. Install browsers: `cd src/A2S.Web && npx playwright install`
3. Check `.env.local` has valid Clerk key
4. Run in headed mode to see what's happening: `npm run test:e2e:headed`

## Port Reference

| Service | Port | URL |
|---------|------|-----|
| Frontend (Vite) | 5173 | http://localhost:5173 |
| Backend API | 5001 | https://localhost:5001 |
| Backend API (HTTP) | 5000 | http://localhost:5000 |
| PostgreSQL | 5432 | localhost:5432 |
| pgAdmin | 5050 | http://localhost:5050 |
| Swagger UI | 5001 | https://localhost:5001/swagger |

## Environment Variables

### Frontend (`.env.local`)

Required for the frontend to work:

```env
VITE_CLERK_PUBLISHABLE_KEY=pk_test_your_clerk_key_here
VITE_API_BASE_URL=https://localhost:5001/api/v1
```

### Backend (`appsettings.Development.json`)

Already configured, but you can modify:
- Database connection string
- JWT settings
- Logging levels

## Available npm Scripts (Root Directory)

| Command | Description |
|---------|-------------|
| `npm start` | Start database, API, and frontend all at once |
| `npm run start:db` | Start PostgreSQL in Docker |
| `npm run start:api` | Start .NET API |
| `npm run start:web` | Start Vite frontend dev server |
| `npm run stop:db` | Stop PostgreSQL container |
| `npm run test:e2e` | Run Playwright E2E tests (headless) |
| `npm run test:e2e:headed` | Run E2E tests with visible browser |
| `npm run test:e2e:ui` | Run E2E tests in interactive UI mode |
| `npm run test:backend` | Run .NET backend tests |
| `npm run build` | Build both API and frontend |
| `npm run setup` | First-time setup (database, dependencies) |

## Development Workflow

1. **Start everything**: `npm start`
2. **Open frontend**: http://localhost:5173
3. **Check Swagger API docs**: https://localhost:5001/swagger
4. **Make changes** to code (hot reload enabled)
5. **Run tests**: `npm run test:e2e:headed`
6. **Stop database when done**: `npm run stop:db`

## Next Steps

- Configure GitHub/Google OAuth in Clerk dashboard
- Review the task list: [task-list.md](task-list.md)
- Check E2E test documentation: [src/A2S.Web/tests/e2e/README.md](src/A2S.Web/tests/e2e/README.md)
