/** Mirrors `A2S.Application.DTOs.DayBudgetDto`: weekly hard sets, cap warnings and the time estimate. */

export interface VolumeWarningDto {
  bucket: string;
  sets: number;
  cap: number;
  message: string;
}

export interface ExerciseTimeDto {
  name: string;
  minutes: number;
}

export interface SessionTimeEstimateDto {
  totalMinutes: number;
  warmUpMinutes: number;
  perExercise: ExerciseTimeDto[];
  overBudget: boolean;
  budgetMinutes: number;
}

export interface DayBudgetDto {
  hardSetsThisWeek: Record<string, number>;
  volumeWarnings: VolumeWarningDto[];
  time: SessionTimeEstimateDto;
}
