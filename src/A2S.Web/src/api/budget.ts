import apiClient from './apiClient';
import type { DayBudgetDto } from '@/types/budget';

/** `GET /api/v1/workouts/{id}/days/{day}/budget?week=`: weekly hard sets, cap warnings, time estimate. */
export const budgetApi = {
  getDayBudget: async (workoutId: string, day: number, week?: number): Promise<DayBudgetDto> => {
    const response = await apiClient.get<DayBudgetDto>(`/workouts/${workoutId}/days/${day}/budget`, {
      params: week !== undefined ? { week } : undefined,
    });
    return response.data;
  },
};
