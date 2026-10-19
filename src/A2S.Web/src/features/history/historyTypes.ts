/** Shapes returned by GET /workouts/history, as the history page consumes them. */

export interface CompletedSetDto {
  setNumber: number;
  weight: number;
  weightUnit: string;
  actualReps: number;
  wasAmrap: boolean;
}

export interface WeeklyPerformanceDto {
  weekNumber: number;
  blockNumber: number;
  completedAt: string | null;
  isDeloadWeek: boolean;
  totalVolume: number;
  averageWeight: number;
  totalReps: number;
  setsCompleted: number;
  amrapReps: number | null;
  sets: CompletedSetDto[];
}

export interface ExerciseHistoryDto {
  exerciseId: string;
  name: string;
  progressionType: string;
  assignedDay: number;
  category: string;
  equipment: string;
  currentWeight: number;
  weightUnit: string;
  currentSets: number;
  targetSets: number;
  trainingMax: number | null;
  weeklyHistory: WeeklyPerformanceDto[];
}

export interface ExercisePerformanceHistoryDto {
  exerciseId: string;
  completedAt: string;
  completedSets: CompletedSetDto[];
}

export interface WorkoutActivityDto {
  day: string;
  dayNumber: number;
  weekNumber: number;
  blockNumber: number;
  completedAt: string;
  isDeloadWeek: boolean;
  performances: ExercisePerformanceHistoryDto[];
}

export interface WorkoutHistoryDto {
  workoutId: string;
  workoutName: string;
  variant: string;
  totalWeeks: number;
  currentWeek: number;
  currentBlock: number;
  daysPerWeek: number;
  startedAt: string | null;
  totalWorkoutsCompleted: number;
  completedActivities: WorkoutActivityDto[];
  exerciseHistories: ExerciseHistoryDto[];
}
