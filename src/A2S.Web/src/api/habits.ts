import apiClient from './apiClient';
import type { LocalDate } from '@/lib/localDate';
import type {
  CreateHabitRequest,
  HabitDto,
  HabitHistoryDto,
  HabitLogDto,
  HabitsTodayDto,
  IncrementHabitRequest,
  SetHabitLogRequest,
  UpdateHabitRequest,
} from '@/types/habits';

/** `/api/v1/habits`: definitions, per-day logs and the streak. */
export const habitsApi = {
  list: async (includeArchived = false): Promise<HabitDto[]> => {
    const response = await apiClient.get<HabitDto[]>('/habits', { params: { includeArchived } });
    return response.data;
  },

  create: async (request: CreateHabitRequest): Promise<HabitDto> => {
    const response = await apiClient.post<HabitDto>('/habits', request);
    return response.data;
  },

  update: async (id: string, request: UpdateHabitRequest): Promise<HabitDto> => {
    const response = await apiClient.put<HabitDto>(`/habits/${id}`, request);
    return response.data;
  },

  archive: async (id: string): Promise<void> => {
    await apiClient.post(`/habits/${id}/archive`);
  },

  unarchive: async (id: string): Promise<void> => {
    await apiClient.post(`/habits/${id}/unarchive`);
  },

  getToday: async (date?: LocalDate): Promise<HabitsTodayDto> => {
    const response = await apiClient.get<HabitsTodayDto>('/habits/today', { params: date ? { date } : undefined });
    return response.data;
  },

  getHistory: async (from: LocalDate, to: LocalDate): Promise<HabitHistoryDto> => {
    const response = await apiClient.get<HabitHistoryDto>('/habits/history', { params: { from, to } });
    return response.data;
  },

  setLog: async (id: string, date: LocalDate, request: SetHabitLogRequest): Promise<HabitLogDto> => {
    const response = await apiClient.put<HabitLogDto>(`/habits/${id}/logs/${date}`, request);
    return response.data;
  },

  increment: async (id: string, date: LocalDate, request: IncrementHabitRequest): Promise<HabitLogDto> => {
    const response = await apiClient.post<HabitLogDto>(`/habits/${id}/logs/${date}/increment`, request);
    return response.data;
  },
};
