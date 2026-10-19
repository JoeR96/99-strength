/** Response shapes for GET /workouts/:id/simulate. */
export interface SimulationDataPoint {
  session: number;
  week: number;
  block: number;
  trainingMax: number | null;
  trainingMaxUnit: string | null;
  currentWeight: number | null;
  currentWeightUnit: string | null;
  summary: {
    type: string;
    details: Record<string, string>;
  };
}

export interface ExerciseSimulationSeries {
  exerciseId: string;
  exerciseName: string;
  progressionType: string;
  dataPoints: SimulationDataPoint[];
}

export interface SimulationResult {
  workoutName: string;
  variant: string;
  startWeek: number;
  endWeek: number;
  totalWeeks: number;
  exerciseTimeSeries: ExerciseSimulationSeries[];
}
