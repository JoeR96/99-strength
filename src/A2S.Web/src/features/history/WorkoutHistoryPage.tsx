import { useState, useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Navbar } from '@/components/layout/Navbar';
import { apiClient } from '@/api';
import {
  GitHubStyleCalendar,
  WorkoutActivityDetail,
  ExerciseProgressView,
  type WorkoutActivityDto,
  type WorkoutHistoryDto,
  type ExerciseHistoryDto,
} from './WorkoutHistoryComponents';

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
  const calendarData = useMemo(() => {
    if (!history || !history.startedAt) return [];

    const startDate = new Date(history.startedAt);
    const now = new Date();
    const months: { month: number; year: number; days: { date: Date; activity: WorkoutActivityDto | null }[] }[] = [];

    // Create a map of activities by date (use local date to match calendar display)
    const activityMap = new Map<string, WorkoutActivityDto>();
    history.completedActivities.forEach(activity => {
      // Parse the UTC date and convert to local date string for matching
      const completedDate = new Date(activity.completedAt);
      const dateKey = completedDate.toDateString();
      activityMap.set(dateKey, activity);
    });

    // Generate months from start to now
    let currentMonth = new Date(startDate.getFullYear(), startDate.getMonth(), 1);
    while (currentMonth <= now) {
      const month = currentMonth.getMonth();
      const year = currentMonth.getFullYear();
      const daysInMonth = new Date(year, month + 1, 0).getDate();
      const firstDayOfWeek = new Date(year, month, 1).getDay();

      const days: { date: Date; activity: WorkoutActivityDto | null }[] = [];

      // Add empty cells for days before the 1st
      for (let i = 0; i < firstDayOfWeek; i++) {
        days.push({ date: new Date(0), activity: null });
      }

      // Add days of the month
      for (let day = 1; day <= daysInMonth; day++) {
        const date = new Date(year, month, day);
        const activity = activityMap.get(date.toDateString()) || null;
        days.push({ date, activity });
      }

      months.push({ month, year, days });
      currentMonth = new Date(year, month + 1, 1);
    }

    return months;
  }, [history]);

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
        <div className="container-page py-8">
          <div className="flex items-center justify-center h-64">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
          </div>
        </div>
      </div>
    );
  }

  if (error || !history) {
    return (
      <div className="min-h-screen bg-background">
        <Navbar />
        <div className="container-page py-8">
          <div className="text-center py-12">
            <h2 className="text-xl font-semibold text-foreground mb-2">No Workout History</h2>
            <p className="text-muted-foreground">
              Complete some workouts to see your history and progress here.
            </p>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <main className="container-page py-8">
        {/* Header */}
        <div className="flex items-center justify-between mb-8">
          <div>
            <h1 className="text-3xl font-bold text-foreground">{history.workoutName}</h1>
            <p className="text-muted-foreground mt-1">
              Week {history.currentWeek} of {history.totalWeeks} · Block {history.currentBlock} · {history.totalWorkoutsCompleted} workouts completed
            </p>
          </div>
          <button
            onClick={handleExportCSV}
            className="px-4 py-2 rounded-lg bg-secondary text-secondary-foreground font-medium hover:bg-secondary/80 transition-colors flex items-center gap-2"
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
            </svg>
            Export CSV
          </button>
        </div>

        {/* View Mode Toggle */}
        <div className="flex gap-2 mb-6">
          <button
            onClick={() => { setViewMode('calendar'); setSelectedExercise(null); }}
            className={`px-4 py-2 rounded-lg font-medium transition-colors ${
              viewMode === 'calendar'
                ? 'bg-primary text-primary-foreground'
                : 'bg-muted text-muted-foreground hover:bg-muted/80'
            }`}
          >
            Activity Calendar
          </button>
          <button
            onClick={() => setViewMode('exercise')}
            className={`px-4 py-2 rounded-lg font-medium transition-colors ${
              viewMode === 'exercise'
                ? 'bg-primary text-primary-foreground'
                : 'bg-muted text-muted-foreground hover:bg-muted/80'
            }`}
          >
            Exercise Progress
          </button>
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
