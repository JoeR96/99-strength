import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { SignedIn, SignedOut } from '@clerk/clerk-react';
import { useInitializeApiClient } from './api';
import { ErrorBoundary } from './components/shared/ErrorBoundary';
import { ProtectedRoute } from './components/shared/ProtectedRoute';
import { LoginPage } from './features/auth/LoginPage';
import { SignUpPage } from './features/auth/SignUpPage';
import { DashboardPage } from './features/auth/DashboardPage';
import { SetupWizard } from './features/workout/SetupWizard';
import { WorkoutDashboard } from './features/workout/WorkoutDashboard';
import { WorkoutSession } from './features/workout/WorkoutSession';
import { ProgramsPage } from './features/programs/ProgramsPage';
import { ExerciseLibraryPage } from './features/exercises/ExerciseLibraryPage';
import { HevyManagementPage } from './features/hevy/HevyManagementPage';
import { HevyDataPage } from './features/hevy/HevyDataPage';
import { SettingsPage } from './features/settings/SettingsPage';
import { WorkoutHistoryPage } from './features/history/WorkoutHistoryPage';
import { SimulationPage } from './features/workout/SimulationPage';

function AppContent() {
  // Initialize API client with Clerk auth
  useInitializeApiClient();

  return (
    <ErrorBoundary>
      <Routes>
        {/* Public routes */}
        <Route
          path="/sign-in/*"
          element={
            <>
              <SignedIn>
                <Navigate to="/dashboard" replace />
              </SignedIn>
              <SignedOut>
                <LoginPage />
              </SignedOut>
            </>
          }
        />
        <Route
          path="/sign-up/*"
          element={
            <>
              <SignedIn>
                <Navigate to="/dashboard" replace />
              </SignedIn>
              <SignedOut>
                <SignUpPage />
              </SignedOut>
            </>
          }
        />

        {/* Protected routes */}
        <Route path="/dashboard" element={<ProtectedRoute><DashboardPage /></ProtectedRoute>} />
        <Route path="/workout" element={<ProtectedRoute><WorkoutDashboard /></ProtectedRoute>} />
        <Route
          path="/workout/session/:day"
          element={
            <ProtectedRoute>
              <ErrorBoundary>
                <WorkoutSession />
              </ErrorBoundary>
            </ProtectedRoute>
          }
        />
        <Route path="/setup" element={<ProtectedRoute><SetupWizard /></ProtectedRoute>} />
        <Route path="/programs" element={<ProtectedRoute><ProgramsPage /></ProtectedRoute>} />
        <Route path="/exercises" element={<ProtectedRoute><ExerciseLibraryPage /></ProtectedRoute>} />
        <Route path="/hevy" element={<ProtectedRoute><HevyManagementPage /></ProtectedRoute>} />
        <Route path="/hevy/data" element={<ProtectedRoute><HevyDataPage /></ProtectedRoute>} />
        <Route path="/settings" element={<ProtectedRoute><SettingsPage /></ProtectedRoute>} />
        <Route path="/history" element={<ProtectedRoute><WorkoutHistoryPage /></ProtectedRoute>} />
        <Route path="/simulate" element={<ProtectedRoute><SimulationPage /></ProtectedRoute>} />

        {/* Default route */}
        <Route
          path="/"
          element={
            <>
              <SignedIn>
                <Navigate to="/dashboard" replace />
              </SignedIn>
              <SignedOut>
                <Navigate to="/sign-in" replace />
              </SignedOut>
            </>
          }
        />

        {/* 404 fallback */}
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </ErrorBoundary>
  );
}

function App() {
  return (
    <BrowserRouter>
      <AppContent />
    </BrowserRouter>
  );
}

export default App;
