import { useState } from 'react';
import { Navbar } from '@/components/layout/Navbar';
import { apiClient } from '@/api';
import { workoutTemplates } from '@/data/workoutTemplates';
import { useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import type { WorkoutDto, ExerciseDto, LinearProgressionDto, RepsPerSetProgressionDto, MinimalSetsProgressionDto } from '@/types/workout';

// Performance scenarios based on E2E test distribution
// ~60% success, ~25% maintained, ~15% failed
type PerformanceScenario = {
  name: string;
  outcome: 'Success' | 'Maintained' | 'Failed';
  amrapDelta: number;
  repsPerSetReps: number;
};

const scenarios: PerformanceScenario[] = [
  // Success scenarios (60%)
  { name: 'Strong Success', outcome: 'Success', amrapDelta: 5, repsPerSetReps: 15 },
  { name: 'Good Success', outcome: 'Success', amrapDelta: 3, repsPerSetReps: 15 },
  { name: 'Moderate Success', outcome: 'Success', amrapDelta: 2, repsPerSetReps: 15 },
  { name: 'Slight Success', outcome: 'Success', amrapDelta: 1, repsPerSetReps: 15 },
  { name: 'Success Variant 1', outcome: 'Success', amrapDelta: 4, repsPerSetReps: 15 },
  { name: 'Success Variant 2', outcome: 'Success', amrapDelta: 2, repsPerSetReps: 15 },
  // Maintained scenarios (25%)
  { name: 'Maintained Exact', outcome: 'Maintained', amrapDelta: 0, repsPerSetReps: 12 },
  { name: 'Maintained Mid', outcome: 'Maintained', amrapDelta: 0, repsPerSetReps: 10 },
  { name: 'Maintained Variant', outcome: 'Maintained', amrapDelta: 0, repsPerSetReps: 11 },
  // Failed scenarios (15%)
  { name: 'Failed Slight', outcome: 'Failed', amrapDelta: -1, repsPerSetReps: 7 },
  { name: 'Failed Moderate', outcome: 'Failed', amrapDelta: -2, repsPerSetReps: 6 },
];

// A2S spreadsheet weekly data
const spreadsheetWeeklyData: { intensity: number; sets: number; reps: number; isDeload: boolean }[] = [
  { intensity: 0, sets: 0, reps: 0, isDeload: false }, // Week 0 placeholder
  // Block 1
  { intensity: 75, sets: 5, reps: 10, isDeload: false },
  { intensity: 85, sets: 4, reps: 8, isDeload: false },
  { intensity: 90, sets: 3, reps: 6, isDeload: false },
  { intensity: 80, sets: 5, reps: 9, isDeload: false },
  { intensity: 85, sets: 4, reps: 7, isDeload: false },
  { intensity: 90, sets: 3, reps: 5, isDeload: false },
  { intensity: 65, sets: 5, reps: 10, isDeload: true },
  // Block 2
  { intensity: 85, sets: 4, reps: 8, isDeload: false },
  { intensity: 90, sets: 3, reps: 6, isDeload: false },
  { intensity: 95, sets: 2, reps: 4, isDeload: false },
  { intensity: 85, sets: 4, reps: 7, isDeload: false },
  { intensity: 90, sets: 3, reps: 5, isDeload: false },
  { intensity: 95, sets: 2, reps: 3, isDeload: false },
  { intensity: 65, sets: 5, reps: 10, isDeload: true },
  // Block 3
  { intensity: 90, sets: 3, reps: 6, isDeload: false },
  { intensity: 95, sets: 2, reps: 4, isDeload: false },
  { intensity: 100, sets: 1, reps: 2, isDeload: false },
  { intensity: 95, sets: 2, reps: 4, isDeload: false },
  { intensity: 100, sets: 1, reps: 2, isDeload: false },
  { intensity: 105, sets: 1, reps: 2, isDeload: false },
  { intensity: 65, sets: 5, reps: 10, isDeload: true },
];

function getScenarioForExercise(exerciseIndex: number, week: number, day: number, counter: number): PerformanceScenario {
  const index = (counter + exerciseIndex + (week * 4) + day) % scenarios.length;
  return scenarios[index];
}

function getAmrapRepsForWeek(week: number, scenario: PerformanceScenario): number {
  const weekData = spreadsheetWeeklyData[week];
  return Math.max(1, weekData.reps + scenario.amrapDelta);
}

export function SettingsPage() {
  const [isSeeding, setIsSeeding] = useState(false);
  const [seedProgress, setSeedProgress] = useState('');
  const [isExporting, setIsExporting] = useState(false);
  const queryClient = useQueryClient();

  const handleSeedData = async () => {
    if (isSeeding) return;

    const confirmed = window.confirm(
      'This will create a new 4-day program and fill weeks 1-17 with random workout data. ' +
      'Any existing active workout will be deleted. Continue?'
    );

    if (!confirmed) return;

    setIsSeeding(true);
    setSeedProgress('Starting seed process...');

    try {
      // 1. Delete any existing workouts
      setSeedProgress('Deleting existing workouts...');
      const existingWorkouts = await apiClient.get<{ id: string }[]>('/workouts');
      for (const workout of existingWorkouts.data) {
        await apiClient.delete(`/workouts/${workout.id}`);
      }

      // 2. Create workout from 4-day template
      setSeedProgress('Creating 4-day workout program...');
      const template = workoutTemplates.find(t => t.id === 'four-day-hypertrophy');
      if (!template) throw new Error('Template not found');

      const createResponse = await apiClient.post('/workouts', {
        name: 'Seeded 4-Day Program',
        variant: template.variant,
        totalWeeks: template.totalWeeks,
        exercises: template.exercises,
      });

      const workoutId = createResponse.data.id;
      setSeedProgress(`Workout created with ID: ${workoutId}`);

      // 3. Complete weeks 1-17 with random data
      let exerciseCounter = 0;
      const totalDays = 17 * 4; // 17 weeks * 4 days
      let completedDays = 0;

      for (let week = 1; week <= 17; week++) {
        for (let day = 1; day <= 4; day++) {
          completedDays++;
          setSeedProgress(`Completing Week ${week}, Day ${day} (${completedDays}/${totalDays})...`);

          // Get current workout state
          const workoutResponse = await apiClient.get<WorkoutDto>('/workouts/current');
          const workout = workoutResponse.data;

          // Get exercises for this day
          const dayExercises = workout.exercises.filter(e => e.assignedDay === day);

          // Build performance data for each exercise
          const performances = dayExercises.map((exercise: ExerciseDto, idx: number) => {
            const scenario = getScenarioForExercise(idx, week, day, exerciseCounter);
            exerciseCounter++;

            return buildPerformanceForExercise(exercise, week, scenario);
          });

          // Complete the day
          await apiClient.post(`/workouts/${workoutId}/days/${day}/complete`, {
            performances,
          });

          // Small delay to not overwhelm the server
          await new Promise(resolve => setTimeout(resolve, 50));
        }
      }

      // 4. Ensure the workout is still active (re-activate if needed)
      setSeedProgress('Ensuring workout is active...');
      try {
        await apiClient.post(`/workouts/${workoutId}/activate`);
      } catch {
        // May already be active, which is fine
      }

      setSeedProgress('Seed complete! Program is now at Week 18, Day 1.');
      toast.success('Successfully seeded workout data for weeks 1-17!');
      queryClient.invalidateQueries({ queryKey: ['workout'] });
      queryClient.invalidateQueries({ queryKey: ['workouts'] });
    } catch (error) {
      console.error('Seed error:', error);
      setSeedProgress(`Error: ${error instanceof Error ? error.message : 'Unknown error'}`);
      toast.error('Failed to seed data');
    } finally {
      setIsSeeding(false);
    }
  };

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
      a.download = `a2s-program-export-${new Date().toISOString().split('T')[0]}.json`;
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
      <main className="container-apple py-8">
        <div className="mb-8">
          <h1 className="text-3xl font-bold text-foreground">Settings</h1>
          <p className="text-muted-foreground mt-2">Manage your application settings and data</p>
        </div>

        <div className="grid gap-6">
          {/* Seed Data Section */}
          <div className="rounded-xl border border-border bg-card p-6">
            <h2 className="text-xl font-semibold text-foreground mb-2">Seed Test Data</h2>
            <p className="text-muted-foreground mb-4">
              Create a 4-day workout program and fill weeks 1-17 with randomly generated workout data.
              This is useful for testing the app with realistic data on a second account.
            </p>
            <p className="text-sm text-muted-foreground mb-4">
              The data is generated using the same distribution as E2E tests:
              ~60% success, ~25% maintained, ~15% failed outcomes.
            </p>

            {seedProgress && (
              <div className="mb-4 p-3 rounded-lg bg-muted/50 text-sm font-mono text-foreground">
                {seedProgress}
              </div>
            )}

            <button
              onClick={handleSeedData}
              disabled={isSeeding}
              className="px-4 py-2 rounded-lg bg-primary text-primary-foreground font-medium hover:bg-primary/90 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              {isSeeding ? 'Seeding...' : 'Seed 4-Day Template (Weeks 1-17)'}
            </button>
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

function buildPerformanceForExercise(
  exercise: ExerciseDto,
  week: number,
  scenario: PerformanceScenario
) {
  const progression = exercise.progression;
  const completedSets: { setNumber: number; weight: number; weightUnit: number; actualReps: number; wasAmrap: boolean }[] = [];

  if (progression.type === 'Linear') {
    const linear = progression as LinearProgressionDto;
    const weekData = spreadsheetWeeklyData[week];
    const workingWeight = Math.round(linear.trainingMax.value * (weekData.intensity / 100));

    // Create sets based on week data
    for (let setNum = 1; setNum <= weekData.sets; setNum++) {
      const isAmrap = setNum === weekData.sets;
      const reps = isAmrap ? getAmrapRepsForWeek(week, scenario) : weekData.reps;

      completedSets.push({
        setNumber: setNum,
        weight: workingWeight,
        weightUnit: linear.trainingMax.unit,
        actualReps: reps,
        wasAmrap: isAmrap,
      });
    }
  } else if (progression.type === 'RepsPerSet') {
    const repsPerSet = progression as RepsPerSetProgressionDto;
    const weight = repsPerSet.currentWeight;
    const weightUnit = repsPerSet.weightUnit === 'Kilograms' ? 1 : 2;

    for (let setNum = 1; setNum <= repsPerSet.currentSetCount; setNum++) {
      completedSets.push({
        setNumber: setNum,
        weight: weight,
        weightUnit: weightUnit,
        actualReps: scenario.repsPerSetReps,
        wasAmrap: false,
      });
    }
  } else if (progression.type === 'MinimalSets') {
    const minimalSets = progression as MinimalSetsProgressionDto;
    const weight = minimalSets.currentWeight;
    const weightUnit = minimalSets.weightUnit === 'Kilograms' ? 1 : 2;
    const repsPerSet = Math.ceil(minimalSets.targetTotalReps / minimalSets.currentSetCount);

    for (let setNum = 1; setNum <= minimalSets.currentSetCount; setNum++) {
      completedSets.push({
        setNumber: setNum,
        weight: weight,
        weightUnit: weightUnit,
        actualReps: repsPerSet,
        wasAmrap: false,
      });
    }
  }

  return {
    exerciseId: exercise.id,
    completedSets,
  };
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
