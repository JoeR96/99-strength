import apiClient from './apiClient';

/**
 * Typed client for the ready-made plans a person can start with one tap
 * (`GET /plans/suggested`, `POST /plans/suggested/cut-to-85`).
 */

export type SessionKind = 'Rest' | 'Lift' | 'Cardio' | 'Mobility';

export interface SuggestedPlanInput {
  key: 'benchTm' | 'squatTm' | 'ohpTm' | 'deadliftTm' | 'startWeightKg';
  label: string;
  unit: string;
  default: number | null;
  minimum: number;
  maximum: number;
}

export interface SuggestedSessionPreview {
  /** 0 = Sunday … 6 = Saturday, as .NET's DayOfWeek serialises. */
  day: number;
  kind: SessionKind;
  label: string;
}

export interface SuggestedDayPreview {
  label: string;
  exercises: string[];
  estimatedMinutes: number;
  overBudget: boolean;
}

export interface SuggestedPlanPreview {
  key: string;
  name: string;
  description: string;
  /** yyyy-MM-dd, always a Monday. */
  startDate: string;
  inputs: SuggestedPlanInput[];
  schedule: SuggestedSessionPreview[];
  liftingDays: SuggestedDayPreview[];
  habits: string[];
  checkpoints: { date: string; targetKg: number; note: string | null }[];
  alreadyStarted: boolean;
}

export interface StartCutTo85Request {
  benchTm: number;
  squatTm: number;
  ohpTm: number;
  deadliftTm: number;
  /** Defaults to 95 on the server when omitted. */
  startWeightKg?: number;
}

export interface ScheduleWarning {
  code: string;
  message: string;
}

export interface StartSuggestedPlanResult {
  planId: string;
  workoutId: string | null;
  /** yyyy-MM-dd */
  startDate: string;
  /** True when an earlier start was returned instead of creating a second plan. */
  alreadyStarted: boolean;
  warnings: ScheduleWarning[];
}

export const CUT_TO_85_KEY = 'cut-to-85';
export const DEFAULT_START_WEIGHT_KG = 95;
export const TRAINING_MAX_MIN_KG = 20;
export const TRAINING_MAX_MAX_KG = 400;
export const START_WEIGHT_MIN_KG = 30;
export const START_WEIGHT_MAX_KG = 300;

/** The first Monday on or after `date` (a Monday is itself). Mirrors `LocalDates.ComingMonday`. */
export function comingMonday(date: Date): Date {
  const result = new Date(date.getFullYear(), date.getMonth(), date.getDate());
  const daysUntil = (1 - result.getDay() + 7) % 7;
  result.setDate(result.getDate() + daysUntil);
  return result;
}

/** "Monday 16 Nov" in en-GB. */
export function formatStartDay(date: Date): string {
  return date.toLocaleDateString('en-GB', { weekday: 'long', day: 'numeric', month: 'short' });
}

export const suggestedPlansApi = {
  list: async (): Promise<SuggestedPlanPreview[]> => {
    const response = await apiClient.get<SuggestedPlanPreview[]>('/plans/suggested');
    return response.data;
  },

  startCutTo85: async (request: StartCutTo85Request): Promise<StartSuggestedPlanResult> => {
    const response = await apiClient.post<StartSuggestedPlanResult>(
      `/plans/suggested/${CUT_TO_85_KEY}`,
      request
    );
    return response.data;
  },
};

export default suggestedPlansApi;
