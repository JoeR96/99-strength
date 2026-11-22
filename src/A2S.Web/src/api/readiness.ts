import apiClient from './apiClient';
import type { LocalDate } from '@/lib/localDate';
import type { ReadinessColour, ReadinessDto } from '@/types/readiness';

/** `/api/v1/readiness`: one tap a day (Green / Yellow / Red). */
export const readinessApi = {
  get: async (date: LocalDate): Promise<ReadinessDto> =>
    (await apiClient.get<ReadinessDto>('/readiness', { params: { date } })).data,

  set: async (date: LocalDate, colour: ReadinessColour): Promise<ReadinessDto> =>
    (await apiClient.put<ReadinessDto>(`/readiness/${date}`, { colour })).data,

  /** On a red day: keep the finisher (the undo) or skip it again. */
  setFinisher: async (date: LocalDate, keep: boolean): Promise<ReadinessDto> =>
    (await apiClient.put<ReadinessDto>(`/readiness/${date}/finisher`, { keep })).data,
};
