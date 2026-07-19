import { useQuery } from '@tanstack/react-query';
import { useEffect, useRef } from 'react';
import { apiClient } from '@/api';
import { ExerciseHistoryChart } from './ExerciseHistoryChart';
import { muscleGroupStyle } from '@/lib/muscleGroupStyles';

/**
 * Exercise data structure
 */
export interface Exercise {
  id: string;
  title: string;
  muscle_group: string;
  equipment: string;
  is_custom: boolean;
}

/**
 * Exercise history types from API
 */
interface CompletedSetDto {
  setNumber: number;
  weight: number;
  weightUnit: string;
  actualReps: number;
  wasAmrap: boolean;
}

interface ExerciseSessionDto {
  workoutId: string;
  workoutName: string;
  weekNumber: number;
  blockNumber: number;
  completedAt: string;
  progressionType: string;
  sessionVolume: number;
  sets: CompletedSetDto[];
}

interface AggregatedExerciseHistoryDto {
  exerciseName: string;
  totalSessions: number;
  totalVolume: number;
  totalSets: number;
  totalReps: number;
  personalRecordWeight: number;
  personalRecordVolume: number;
  weightUnit: string;
  firstPerformed: string | null;
  lastPerformed: string | null;
  sessions: ExerciseSessionDto[];
}

/**
 * Muscle group display configuration (label + icon only — colour identity lives in
 * `lib/muscleGroupStyles.ts` as a token-driven categorical lookup, `muscleGroupStyle()`).
 */
export const MUSCLE_GROUP_CONFIG: Record<string, { label: string; icon: string }> = {
  abdominals: { label: 'Abdominals', icon: '🎯' },
  adductors: { label: 'Adductors', icon: '🦵' },
  back: { label: 'Back', icon: '🔙' },
  biceps: { label: 'Biceps', icon: '💪' },
  calves: { label: 'Calves', icon: '🦶' },
  cardio: { label: 'Cardio', icon: '❤️' },
  chest: { label: 'Chest', icon: '🫁' },
  forearms: { label: 'Forearms', icon: '🤜' },
  full_body: { label: 'Full Body', icon: '🏋️' },
  glutes: { label: 'Glutes', icon: '🍑' },
  hamstrings: { label: 'Hamstrings', icon: '🦵' },
  lats: { label: 'Lats', icon: '🦅' },
  lower_back: { label: 'Lower Back', icon: '⬇️' },
  neck: { label: 'Neck', icon: '🦒' },
  obliques: { label: 'Obliques', icon: '↔️' },
  other: { label: 'Other', icon: '📦' },
  quadriceps: { label: 'Quadriceps', icon: '🦵' },
  shoulders: { label: 'Shoulders', icon: '🔝' },
  traps: { label: 'Traps', icon: '⬆️' },
  triceps: { label: 'Triceps', icon: '💪' },
};

/**
 * Equipment display configuration
 */
export const EQUIPMENT_CONFIG: Record<string, { label: string; icon: string }> = {
  barbell: { label: 'Barbell', icon: '🏋️' },
  bodyweight: { label: 'Bodyweight', icon: '🤸' },
  cable: { label: 'Cable', icon: '🔗' },
  dumbbell: { label: 'Dumbbell', icon: '🏋️' },
  ez_bar: { label: 'EZ Bar', icon: '🔄' },
  kettlebell: { label: 'Kettlebell', icon: '🔔' },
  machine: { label: 'Machine', icon: '⚙️' },
  none: { label: 'No Equipment', icon: '👐' },
  other: { label: 'Other', icon: '📦' },
  plate: { label: 'Plate', icon: '⭕' },
  resistance_band: { label: 'Resistance Band', icon: '🎀' },
  smith_machine: { label: 'Smith Machine', icon: '🔩' },
  suspension: { label: 'Suspension', icon: '⛓️' },
  trap_bar: { label: 'Trap Bar', icon: '⬡' },
};

/**
 * Categorized muscle groups for better organization
 */
export const MUSCLE_CATEGORIES = {
  'Upper Body - Push': ['chest', 'shoulders', 'triceps'],
  'Upper Body - Pull': ['back', 'lats', 'biceps', 'forearms', 'traps'],
  'Lower Body': ['quadriceps', 'hamstrings', 'glutes', 'calves', 'adductors'],
  'Core': ['abdominals', 'obliques', 'lower_back'],
  'Other': ['neck', 'cardio', 'full_body', 'other'],
};

/**
 * Exercise Card Component
 */
export function ExerciseCard({ exercise, onClick }: { exercise: Exercise; onClick?: () => void }) {
  const muscleConfig = MUSCLE_GROUP_CONFIG[exercise.muscle_group] || { label: exercise.muscle_group, icon: '📦' };
  const equipmentConfig = EQUIPMENT_CONFIG[exercise.equipment] || { label: exercise.equipment, icon: '📦' };

  return (
    <button
      onClick={onClick}
      className={`p-3 rounded-lg border ${exercise.is_custom ? 'border-primary/30 bg-primary/5' : 'border-border bg-card'} hover:border-primary/50 transition-colors text-left w-full cursor-pointer`}
    >
      <div className="flex items-start justify-between gap-2">
        <h3 className="font-medium text-sm text-foreground leading-tight">{exercise.title}</h3>
        {exercise.is_custom && (
          <span className="text-[10px] px-1.5 py-0.5 rounded bg-primary/20 text-primary font-medium shrink-0">
            Custom
          </span>
        )}
      </div>
      <div className="mt-2 flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
        <span className="px-1.5 py-0.5 rounded" style={muscleGroupStyle(exercise.muscle_group)}>
          {muscleConfig.icon} {muscleConfig.label}
        </span>
        <span className="px-1.5 py-0.5 rounded bg-muted">
          {equipmentConfig.icon} {equipmentConfig.label}
        </span>
      </div>
    </button>
  );
}

/**
 * Exercise List Item Component
 */
export function ExerciseListItem({ exercise, onClick }: { exercise: Exercise; onClick?: () => void }) {
  const muscleConfig = MUSCLE_GROUP_CONFIG[exercise.muscle_group] || { label: exercise.muscle_group, icon: '📦' };
  const equipmentConfig = EQUIPMENT_CONFIG[exercise.equipment] || { label: exercise.equipment, icon: '📦' };

  return (
    <button
      onClick={onClick}
      className={`px-3 py-2 rounded border ${exercise.is_custom ? 'border-primary/30 bg-primary/5' : 'border-border bg-card'} hover:border-primary/50 transition-colors flex items-center gap-4 w-full text-left cursor-pointer`}
    >
      <div className="flex-1 min-w-0">
        <h3 className="font-medium text-sm text-foreground truncate">{exercise.title}</h3>
      </div>
      <div className="flex items-center gap-2 shrink-0">
        <span className="px-1.5 py-0.5 rounded text-xs" style={muscleGroupStyle(exercise.muscle_group)}>
          {muscleConfig.label}
        </span>
        <span className="px-1.5 py-0.5 rounded bg-muted text-xs text-muted-foreground">
          {equipmentConfig.label}
        </span>
        {exercise.is_custom && (
          <span className="text-[10px] px-1.5 py-0.5 rounded bg-primary/20 text-primary font-medium">
            Custom
          </span>
        )}
      </div>
    </button>
  );
}

/**
 * Exercise History Modal Component
 */
export function ExerciseHistoryModal({ exercise, onClose }: { exercise: Exercise; onClose: () => void }) {
  const dialogRef = useRef<HTMLDivElement>(null);
  const { data: history, isLoading, error } = useQuery({
    queryKey: ['exercise-history', exercise.title],
    queryFn: async () => {
      const response = await apiClient.get<AggregatedExerciseHistoryDto>(
        `/workouts/exercises/${encodeURIComponent(exercise.title)}/history`
      );
      return response.data;
    },
  });

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    document.addEventListener('keydown', handleKeyDown);
    dialogRef.current?.focus();
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [onClose]);

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-label={`${exercise.title} exercise history`}
      ref={dialogRef}
      tabIndex={-1}
    >
      <div className="bg-card border border-border rounded-xl shadow-xl max-w-4xl w-full max-h-[90vh] overflow-hidden flex flex-col">
        {/* Header */}
        <div className="flex items-center justify-between p-6 border-b border-border">
          <div>
            <h2 className="text-xl font-semibold text-foreground">{exercise.title}</h2>
            <p className="text-sm text-muted-foreground mt-1">Exercise History</p>
          </div>
          <button
            onClick={onClose}
            className="p-2 rounded-lg hover:bg-muted transition-colors"
          >
            <svg className="w-5 h-5 text-muted-foreground" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto p-6">
          {isLoading && (
            <div className="flex items-center justify-center h-48">
              <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
            </div>
          )}

          {error && (
            <div className="text-center py-12">
              <p className="text-muted-foreground">Failed to load exercise history</p>
            </div>
          )}

          {!isLoading && !error && !history && (
            <div className="text-center py-12">
              <svg className="w-16 h-16 text-muted-foreground/30 mx-auto mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
              </svg>
              <p className="text-muted-foreground">No history found for this exercise</p>
              <p className="text-sm text-muted-foreground mt-2">
                Complete workouts with this exercise to see your progress here.
              </p>
            </div>
          )}

          {history && (
            <div className="space-y-6">
              {/* Stats Overview */}
              <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                <div className="p-4 rounded-lg bg-muted/30">
                  <p className="text-xs text-muted-foreground">Total Sessions</p>
                  <p className="text-2xl font-semibold text-foreground">{history.totalSessions}</p>
                </div>
                <div className="p-4 rounded-lg bg-muted/30">
                  <p className="text-xs text-muted-foreground">Total Volume</p>
                  <p className="text-2xl font-semibold text-foreground">
                    {Math.round(history.totalVolume).toLocaleString()}
                  </p>
                </div>
                <div className="p-4 rounded-lg bg-muted/30">
                  <p className="text-xs text-muted-foreground">PR Weight</p>
                  <p className="text-2xl font-semibold text-foreground">
                    {history.personalRecordWeight} {history.weightUnit === 'Kilograms' ? 'kg' : 'lbs'}
                  </p>
                </div>
                <div className="p-4 rounded-lg bg-muted/30">
                  <p className="text-xs text-muted-foreground">Total Sets</p>
                  <p className="text-2xl font-semibold text-foreground">{history.totalSets}</p>
                </div>
              </div>

              {/* Performance Chart with time filtering and metric selection */}
              {history.sessions.length > 0 && (
                <div className="rounded-xl border border-border bg-card p-6">
                  <h3 className="text-lg font-semibold text-foreground mb-4">Performance Over Time</h3>
                  <ExerciseHistoryChart
                    sessions={history.sessions}
                    weightUnit={history.weightUnit}
                  />
                </div>
              )}

              {/* Session History Table */}
              <div className="rounded-xl border border-border bg-card p-6">
                <h3 className="text-lg font-semibold text-foreground mb-4">Session History</h3>
                <div className="overflow-x-auto">
                  <table className="w-full">
                    <thead>
                      <tr className="border-b border-border">
                        <th className="text-left py-3 px-4 text-sm font-medium text-muted-foreground">Date</th>
                        <th className="text-left py-3 px-4 text-sm font-medium text-muted-foreground">Workout</th>
                        <th className="text-left py-3 px-4 text-sm font-medium text-muted-foreground">Week</th>
                        <th className="text-right py-3 px-4 text-sm font-medium text-muted-foreground">Sets</th>
                        <th className="text-right py-3 px-4 text-sm font-medium text-muted-foreground">Volume</th>
                      </tr>
                    </thead>
                    <tbody>
                      {history.sessions.slice().reverse().map((session, idx) => (
                        <tr key={idx} className="border-b border-border/50">
                          <td className="py-3 px-4 text-sm text-foreground">
                            {new Date(session.completedAt).toLocaleDateString()}
                          </td>
                          <td className="py-3 px-4 text-sm text-muted-foreground">
                            {session.workoutName}
                          </td>
                          <td className="py-3 px-4 text-sm text-muted-foreground">
                            Week {session.weekNumber}
                          </td>
                          <td className="py-3 px-4 text-sm text-right font-mono text-foreground">
                            {session.sets.length}
                          </td>
                          <td className="py-3 px-4 text-sm text-right font-mono text-foreground">
                            {Math.round(session.sessionVolume)}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>

              {/* Recent Sets */}
              {history.sessions.length > 0 && (
                <div className="rounded-xl border border-border bg-card p-6">
                  <h3 className="text-lg font-semibold text-foreground mb-4">
                    Most Recent Session Sets
                  </h3>
                  <div className="flex flex-wrap gap-2">
                    {history.sessions[history.sessions.length - 1].sets.map((set) => (
                      <div
                        key={set.setNumber}
                        className={`px-3 py-2 rounded-lg text-sm ${
                          set.wasAmrap
                            ? 'bg-primary/10 text-primary border border-primary/30'
                            : 'bg-muted/50 text-foreground'
                        }`}
                      >
                        <span className="font-mono">
                          {set.weight}{history.weightUnit === 'Kilograms' ? 'kg' : 'lbs'} × {set.actualReps}
                        </span>
                        {set.wasAmrap && <span className="ml-1 text-xs">(AMRAP)</span>}
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
