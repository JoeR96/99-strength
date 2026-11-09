import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Navbar } from '@/components/layout/Navbar';
import { apiClient } from '@/api';
import { HevySettings } from '@/components/hevy/HevySettings';
import { PushSettings } from './PushSettings';
import { HevySyncSettings } from './HevySyncSettings';
import { ApiTokensSettings } from './ApiTokensSettings';
import toast from 'react-hot-toast';
import type { WorkoutDto, ExerciseDto } from '@/types/workout';

export function SettingsPage() {
  const [isExporting, setIsExporting] = useState(false);

  const handleExportProgram = async () => {
    if (isExporting) return;

    setIsExporting(true);

    try {
      // Try to get current active workout first
      let workout: WorkoutDto | null = null;
      try {
        const response = await apiClient.get<WorkoutDto>('/workouts/current');
        workout = response.data;
      } catch {
        // No active workout, try to get the most recent one
        const allResponse = await apiClient.get<WorkoutDto[]>('/workouts');
        if (allResponse.data && allResponse.data.length > 0) {
          // The /workouts endpoint returns summaries, use the first (most recent)
          workout = allResponse.data[0];
        }
      }

      if (!workout) {
        toast.error('No workout to export');
        return;
      }

      // Build comprehensive export data
      const exportData = buildExportData(workout);

      // Create and download JSON file
      const blob = new Blob([JSON.stringify(exportData, null, 2)], { type: 'application/json' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `99-strength-export-${new Date().toISOString().split('T')[0]}.json`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);

      toast.success('Program exported successfully!');
    } catch (error) {
      console.error('Export error:', error);
      toast.error('Failed to export program');
    } finally {
      setIsExporting(false);
    }
  };

  return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <main className="container-page py-8">
        <div className="mb-8">
          <h1 className="text-3xl font-bold text-foreground">Settings</h1>
          <p className="text-muted-foreground mt-2">Manage your application settings and data</p>
        </div>

        <div className="grid gap-6">
          {/* More: features that have no tab of their own */}
          <div className="rounded-xl border border-border bg-card p-6">
            <h2 className="text-xl font-semibold text-foreground mb-2">More</h2>
            <p className="text-muted-foreground mb-4">
              Pick this week's meals from the cut-friendly recipe set and shop from one summed list.
            </p>
            <div className="flex flex-wrap gap-3">
              <Link
                to="/meals"
                className="inline-flex min-h-11 items-center px-4 py-2 rounded-lg bg-secondary text-secondary-foreground font-medium hover:bg-secondary/80 transition-colors"
              >
                Meals
              </Link>
              <Link
                to="/shopping-list"
                className="inline-flex min-h-11 items-center px-4 py-2 rounded-lg border border-border text-foreground font-medium hover:bg-muted transition-colors"
              >
                Shopping list
              </Link>
            </div>
          </div>

          <PushSettings />

          <HevySettings />

          <HevySyncSettings />

          <ApiTokensSettings />

          {/* Secondary pages that used to sit in the top menu */}
          <div className="rounded-xl border border-border bg-card p-6">
            <h2 className="text-xl font-semibold text-foreground mb-2">More</h2>
            <p className="text-muted-foreground mb-4">Programmes, the exercise library and Hevy data.</p>
            <ul className="grid gap-2 sm:grid-cols-2">
              {[
                { to: '/programs', label: 'Programmes', hint: 'Create, activate or delete a programme' },
                { to: '/exercises', label: 'Exercise library', hint: 'Browse exercises and Hevy history' },
                { to: '/hevy', label: 'Hevy routines', hint: 'Routines pushed to Hevy' },
                { to: '/hevy/data', label: 'Hevy data', hint: 'Charts from your Hevy workouts' },
                // The API drops the simulate routes in Production ([NonProductionOnly]), so the
                // link would open a page that can only fail there.
                ...(import.meta.env.DEV
                  ? [{ to: '/simulate', label: 'Simulator', hint: 'Developer: project progression' }]
                  : []),
              ].map((item) => (
                <li key={item.to}>
                  <Link
                    to={item.to}
                    className="flex min-h-14 flex-col justify-center rounded-lg border border-border px-4 py-3 transition-colors hover:bg-muted"
                  >
                    <span className="font-medium text-foreground">{item.label}</span>
                    <span className="text-xs text-muted-foreground">{item.hint}</span>
                  </Link>
                </li>
              ))}
            </ul>
          </div>

          {/* Export Data Section */}
          <div className="rounded-xl border border-border bg-card p-6">
            <h2 className="text-xl font-semibold text-foreground mb-2">Export Program Data</h2>
            <p className="text-muted-foreground mb-4">
              Export your complete workout program including all exercises, progression settings,
              and workout history. This creates a JSON backup file you can use to restore your data.
            </p>

            <button
              onClick={handleExportProgram}
              disabled={isExporting}
              className="px-4 py-2 rounded-lg bg-secondary text-secondary-foreground font-medium hover:bg-secondary/80 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              {isExporting ? 'Exporting...' : 'Export Current Program'}
            </button>
          </div>
        </div>
      </main>
    </div>
  );
}

function buildExportData(workout: WorkoutDto) {
  return {
    exportVersion: '1.0',
    exportedAt: new Date().toISOString(),
    program: {
      id: workout.id,
      name: workout.name,
      variant: workout.variant,
      totalWeeks: workout.totalWeeks,
      currentWeek: workout.currentWeek,
      currentBlock: workout.currentBlock,
      currentDay: workout.currentDay,
      daysPerWeek: workout.daysPerWeek,
      status: workout.status,
      createdAt: workout.createdAt,
      startedAt: workout.startedAt,
      completedAt: workout.completedAt,
      completedDaysInCurrentWeek: workout.completedDaysInCurrentWeek,
      isWeekComplete: workout.isWeekComplete,
    },
    exercises: workout.exercises.map(exercise => ({
      id: exercise.id,
      name: exercise.name,
      category: exercise.category,
      equipment: exercise.equipment,
      assignedDay: exercise.assignedDay,
      orderInDay: exercise.orderInDay,
      hevyExerciseTemplateId: exercise.hevyExerciseTemplateId,
      progression: exercise.progression,
    })),
    exercisesByDay: groupExercisesByDay(workout.exercises),
    hevyIntegration: {
      routineFolderId: workout.hevyRoutineFolderId,
      syncedRoutines: workout.hevySyncedRoutines,
    },
  };
}

function groupExercisesByDay(exercises: ExerciseDto[]) {
  const grouped: Record<number, ExerciseDto[]> = {};
  for (const exercise of exercises) {
    const day = exercise.assignedDay;
    if (!grouped[day]) grouped[day] = [];
    grouped[day].push(exercise);
  }
  // Sort exercises within each day by order
  for (const day of Object.keys(grouped)) {
    grouped[Number(day)].sort((a, b) => a.orderInDay - b.orderInDay);
  }
  return grouped;
}
