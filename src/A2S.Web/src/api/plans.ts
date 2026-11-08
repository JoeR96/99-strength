import apiClient from './apiClient';
import type { LocalDate } from '@/lib/localDate';
import type {
  AllowanceKind,
  AllowanceUseDto,
  CompleteSessionRequest,
  PlanDayDto,
  SessionCompletionDto,
  TodayDto,
  TrainingPlanDto,
  UseAllowanceRequest,
  WeeklyGoalsDto,
} from '@/types/plan';

export function isNotFound(error: unknown): boolean {
  return (
    typeof error === 'object' &&
    error !== null &&
    'response' in error &&
    (error as { response?: { status?: number } }).response?.status === 404
  );
}

/** `/api/v1/plans`: the Whole Plan beside the A2S2 workout. */
export const plansApi = {
  /** The active plan, or `null` when the user has not started one. */
  getActive: async (): Promise<TrainingPlanDto | null> => {
    try {
      const response = await apiClient.get<TrainingPlanDto>('/plans/active');
      return response.data;
    } catch (error) {
      if (isNotFound(error)) return null;
      throw error;
    }
  },

  /** The given date's sessions, phase and week; `null` when there is no active plan. */
  getToday: async (date?: LocalDate): Promise<TodayDto | null> => {
    try {
      const response = await apiClient.get<TodayDto>('/plans/today', { params: date ? { date } : undefined });
      return response.data;
    } catch (error) {
      if (isNotFound(error)) return null;
      throw error;
    }
  },

  getCalendar: async (year: number, month: number): Promise<PlanDayDto[]> => {
    const response = await apiClient.get<PlanDayDto[]>('/plans/calendar', { params: { year, month } });
    return response.data;
  },

  getWeeklyGoals: async (date?: LocalDate): Promise<WeeklyGoalsDto> => {
    const response = await apiClient.get<WeeklyGoalsDto>('/plans/weekly-goals', {
      params: date ? { date } : undefined,
    });
    return response.data;
  },

  completeSession: async (planId: string, request: CompleteSessionRequest): Promise<SessionCompletionDto> => {
    const response = await apiClient.post<SessionCompletionDto>(`/plans/${planId}/completions`, request);
    return response.data;
  },

  undoCompletion: async (planId: string, completionId: string): Promise<void> => {
    await apiClient.delete(`/plans/${planId}/completions/${completionId}`);
  },

  useAllowance: async (planId: string, request: UseAllowanceRequest): Promise<AllowanceUseDto> => {
    const response = await apiClient.post<AllowanceUseDto>(`/plans/${planId}/allowances`, request);
    return response.data;
  },

  removeAllowanceUse: async (planId: string, date: LocalDate, kind: AllowanceKind): Promise<void> => {
    await apiClient.delete(`/plans/${planId}/allowances`, { params: { date, kind } });
  },
};
