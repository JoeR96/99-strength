import { useState, useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Navbar } from '@/components/layout/Navbar';
import { PageHeader } from '@/components/layout/PageHeader';
import { Button } from '@/components/ui/button';
import { apiClient } from '@/api';
import {
  GitHubStyleCalendar,
  WorkoutActivityDetail,
  ExerciseProgressView,
  type WorkoutActivityDto,
  type WorkoutHistoryDto,
  type ExerciseHistoryDto,
} from './WorkoutHistoryComponents';
import { buildCalendarMonths } from './calendarData';

export function WorkoutHistoryPage() {
  const [selectedExercise, setSelectedExercise] = useState<ExerciseHistoryDto | null>(null);
  const [selectedActivity, setSelectedActivity] = useState<{ activity: WorkoutActivityDto; date: Date } | null>(null);
  const [viewMode, setViewMode] = useState<'calendar' | 'exercise'>('calendar');

  const { data: history, isLoading, error } = useQuery({
    queryKey: ['workout-history'],
    queryFn: async () => {
      const response = await apiClient.get<WorkoutHistoryDto>('/workouts/history');
      return response.data;
    },
  });

  // Build calendar data grouped by month
  const calendarData = useMemo(() => buildCalendarMonths(history), [history]);

  const handleExportCSV = () => {
    if (!history) return;

    const rows: string[][] = [];

    // Header
    rows.push(['Exercise', 'Day', 'Week', 'Block', 'Date', 'Set', 'Weight', 'Unit', 'Reps', 'AMRAP', 'Volume']);

    // Data rows
    history.exerciseHistories.forEach(exercise => {
      exercise.weeklyHistory.forEach(week => {
        week.sets.forEach(set => {
          rows.push([
            exercise.name,
            exercise.assignedDay.toString(),
            week.weekNumber.toString(),
            week.blockNumber.toString(),
            week.completedAt ? new Date(week.completedAt).toLocaleDateString() : '',
            set.setNumber.toString(),
            set.weight.toString(),
            set.weightUnit,
            set.actualReps.toString(),
            set.wasAmrap ? 'Yes' : 'No',
            (set.weight * set.actualReps).toString(),
          ]);
        });
      });
    });

    // Create CSV content
    const csvContent = rows.map(row => row.map(cell => `"${cell}"`).join(',')).join('\n');
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `workout-history-${new Date().toISOString().split('T')[0]}.csv`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  };

  if (isLoading) {
    return (
      <div className="min-h-screen bg-background">
        <Navbar />
        <main className="container-page py-8">
          <div className="flex items-center justify-center h-64">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
          </div>
        </main>
      </div>
    );
  }

  if (error || !history) {
    return (
      <div className="min-h-screen bg-background">
        <Navbar />
        <main className="container-page py-8">
          <div className="text-center py-12">
            <h2 className="text-xl font-semibold text-foreground mb-2">No Workout History</h2>
            <p className="text-muted-foreground">
              Complete some workouts to see your history and progress here.
            </p>
          </div>
        </main>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <main className="container-page py-8">
        {/* Header */}
        <PageHeader
          title={history.workoutName}
          description={`Week ${history.currentWeek} of ${history.totalWeeks} · Block ${history.currentBlock} · ${history.totalWorkoutsCompleted} workouts completed`}
          actions={
            <Button variant="secondary" size="sm" onClick={handleExportCSV}>
              <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
              </svg>
              Export CSV
            </Button>
          }
        />

        {/* View Mode Toggle */}
        <div className="flex gap-2 mb-6" role="group" aria-label="History view">
          <Button
            size="sm"
            variant={viewMode === 'calendar' ? 'default' : 'secondary'}
            aria-pressed={viewMode === 'calendar'}
            onClick={() => { setViewMode('calendar'); setSelectedExercise(null); }}
          >
            Activity Calendar
          </Button>
          <Button
            size="sm"
            variant={viewMode === 'exercise' ? 'default' : 'secondary'}
            aria-pressed={viewMode === 'exercise'}
            onClick={() => setViewMode('exercise')}
          >
            Exercise Progress
          </Button>
        </div>

        {viewMode === 'calendar' ? (
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            <div className="lg:col-span-2">
              <GitHubStyleCalendar
                months={calendarData}
                daysPerWeek={history.daysPerWeek}
                onActivityClick={(activity, date) => setSelectedActivity({ activity, date })}
                selectedDate={selectedActivity?.date}
              />
            </div>
            <div className="lg:col-span-1">
              <WorkoutActivityDetail
                activity={selectedActivity?.activity}
                date={selectedActivity?.date}
                exerciseHistories={history.exerciseHistories}
                onClose={() => setSelectedActivity(null)}
              />
            </div>
          </div>
        ) : (
          <ExerciseProgressView
            exercises={history.exerciseHistories}
            selectedExercise={selectedExercise}
            onSelectExercise={setSelectedExercise}
          />
        )}
      </main>
    </div>
  );
}
