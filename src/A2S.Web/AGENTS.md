# Frontend — A2S.Web

React SPA for the 99-Strength workout tracker. Inherits root `AGENTS.md` for domain model, ubiquitous language, and architecture context.

## Stack

| Tech | Version | Purpose |
|------|---------|---------|
| React | 19 | UI framework |
| TypeScript | 5.9 | Type safety |
| Vite | 7.2 | Build tool, dev server (port 5173) |
| Tailwind CSS | 4.1 | Utility-first styling |
| ShadCN UI | Partial (Button, Card, Dialog, Input, Label, Switch) | Component primitives |
| TanStack Query | 5.x | Server state management |
| React Router DOM | 7.x | Client-side routing |
| Clerk | `@clerk/clerk-react` | Authentication |
| Recharts | 3.x | Charts/graphs |
| @dnd-kit | Drag-and-drop (exercise reordering) |
| Lucide React | Icons |
| Axios | HTTP client |

## Project Structure

```
src/
├── api/              # API client (Axios), typed endpoint functions
│   ├── apiClient.ts  # Base Axios instance with Clerk auth interceptor
│   ├── workouts.ts   # Workout CRUD + progression endpoints
│   ├── users.ts      # User endpoints
│   └── index.ts      # Re-exports
├── components/
│   ├── ui/           # ShadCN primitives (button, card, dialog, etc.)
│   ├── shared/       # App-wide shared components (ErrorBoundary, ProtectedRoute, modals)
│   ├── layout/       # Navbar, page layout
│   └── hevy/         # Hevy-specific components
├── contexts/         # React contexts (ThemeContext, HevyContext)
├── features/         # Feature-folder organized pages
│   ├── auth/         # Login, SignUp, Dashboard pages
│   ├── exercises/    # Exercise library page
│   ├── hevy/         # Hevy sync/data pages
│   ├── history/      # Workout history page
│   ├── programs/     # Programs management page
│   ├── settings/     # Settings page
│   └── workout/      # Setup wizard, workout session, week overview
├── hooks/            # Custom hooks (useAuth, useWorkouts, useHevySync)
├── services/         # Business logic services (hevySyncService, etc.)
├── data/             # Static data (exercise templates, workout templates)
├── types/            # TypeScript type definitions
├── utils/            # Pure utility functions
├── lib/              # Library utilities (cn() for class merging)
├── test/             # Test utilities and setup
├── App.tsx           # Router + providers
├── main.tsx          # Entry point
├── index.css         # Tailwind + theme CSS
└── queryClient.ts    # TanStack Query client config
```

## Conventions

### Components

- **Feature-folder structure**: Pages and related components live under `features/{feature}/`
- **Max 500 lines per file** — decompose into sub-components if exceeding
- **ShadCN primitives** for common UI elements (buttons, cards, dialogs, inputs)
- **Tailwind for styling** — no CSS modules, no styled-components
- **`cn()` utility** from `lib/utils` for conditional class merging (`clsx` + `tailwind-merge`)

### API & Server State

- **TanStack Query** for all server state — no manual `useState` + `useEffect` fetching
- **Axios-based API client** (`api/apiClient.ts`) with Clerk auth token injection
- **Path alias**: `@/` maps to `src/` (configured in `vite.config.ts`)
- **API base URL**: `VITE_API_BASE_URL` env var (default: `https://localhost:5001/api/v1`)

### Auth

- **Clerk** handles all auth — `useAuth()` hook from `@clerk/clerk-react`
- **Token injection**: `setTokenGetter()` sets up automatic Bearer token on Axios requests
- **Protected routes**: `ProtectedRoute` component wraps authenticated pages

### Theming

Two themes via CSS custom properties in `index.css`:

| Theme | Class | Style |
|-------|-------|-------|
| **Retro Arcade** | (default/light) | Dark bg, neon magenta/yellow, pixel fonts (Press Start 2P, VT323), scanline overlay |
| **OSRS** | `.dark` | Brown parchment, gold text, RuneScape UF font |

Theme toggled via `ThemeContext`. Both are dark-background themes despite light/dark naming.

### Storybook

- Storybook 10 configured at port 6006
- Stories colocated with components: `Component.stories.tsx`
- Addons: a11y, docs, vitest

## Build & Dev

```bash
npm run dev           # Vite dev server (port 5173)
npm run build         # TypeScript check + Vite production build
npm test              # Vitest (run once)
npm run test:watch    # Vitest (watch mode)
npm run storybook     # Storybook (port 6006)
npm run lint          # ESLint
npm run format        # Prettier
```

## Testing

- **Vitest** for unit tests — colocated `*.test.ts(x)` files
- **Testing Library** (`@testing-library/react`) for component tests
- **`renderHook`** for custom hook tests
- **Mock API calls** — manual mocks or TanStack Query test utilities
- **No `console.log`** in test files
