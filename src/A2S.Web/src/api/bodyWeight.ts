import apiClient from './apiClient';
import type { LocalDate } from '@/lib/localDate';
import type { BodyWeightEntryDto, WeightTrendDto } from '@/types/bodyWeight';

/** `/api/v1/body-weight`: morning weigh-ins and the smoothed trend. */
export const bodyWeightApi = {
  record: async (date: LocalDate, kg: number): Promise<BodyWeightEntryDto> => {
    const response = await apiClient.put<BodyWeightEntryDto>(`/body-weight/${date}`, { kg });
    return response.data;
  },

  /** Omit both dates for the server default (the last 90 days). */
  getTrend: async (from?: LocalDate, to?: LocalDate): Promise<WeightTrendDto> => {
    const response = await apiClient.get<WeightTrendDto>('/body-weight/trend', {
      params: { ...(from ? { from } : {}), ...(to ? { to } : {}) },
    });
    return response.data;
  },
};
