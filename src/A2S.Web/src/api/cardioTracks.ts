import apiClient from './apiClient';
import type {
  BenchmarkResultDto,
  CardioTrackDetailDto,
  CardioTrackDto,
  CardioTrackTemplateDto,
  HeartRateZonesDto,
  LogTrackSessionRequest,
  TodayCardioDto,
  TrackEvaluationDto,
  UpdateCardioTrackRequest,
} from '@/types/cardioTracks';

/** `/api/v1/cardio-tracks`: cardio progression tracks started from templates. */
export const cardioTracksApi = {
  list: async (): Promise<CardioTrackDto[]> => (await apiClient.get<CardioTrackDto[]>('/cardio-tracks')).data,

  templates: async (): Promise<CardioTrackTemplateDto[]> =>
    (await apiClient.get<CardioTrackTemplateDto[]>('/cardio-tracks/templates')).data,

  today: async (): Promise<TodayCardioDto> => (await apiClient.get<TodayCardioDto>('/cardio-tracks/today')).data,

  get: async (id: string): Promise<CardioTrackDetailDto> => (await apiClient.get<CardioTrackDetailDto>(`/cardio-tracks/${id}`)).data,

  create: async (templateKey: string, name?: string): Promise<CardioTrackDto> =>
    (await apiClient.post<CardioTrackDto>('/cardio-tracks', { templateKey, name: name ?? null })).data,

  update: async (id: string, request: UpdateCardioTrackRequest): Promise<CardioTrackDto> =>
    (await apiClient.put<CardioTrackDto>(`/cardio-tracks/${id}`, request)).data,

  logSession: async (id: string, request: LogTrackSessionRequest): Promise<TrackEvaluationDto> =>
    (await apiClient.post<TrackEvaluationDto>(`/cardio-tracks/${id}/sessions`, request)).data,

  decide: async (id: string, accept: boolean): Promise<CardioTrackDto> =>
    (await apiClient.post<CardioTrackDto>(`/cardio-tracks/${id}/suggestion`, { accept })).data,

  undo: async (id: string): Promise<CardioTrackDto> => (await apiClient.post<CardioTrackDto>(`/cardio-tracks/${id}/undo`)).data,

  pause: async (id: string): Promise<CardioTrackDto> => (await apiClient.post<CardioTrackDto>(`/cardio-tracks/${id}/pause`)).data,

  resume: async (id: string): Promise<CardioTrackDto> => (await apiClient.post<CardioTrackDto>(`/cardio-tracks/${id}/resume`)).data,

  getZones: async (): Promise<HeartRateZonesDto> => (await apiClient.get<HeartRateZonesDto>('/cardio-tracks/heart-rate-zones')).data,

  setZones: async (maxHeartRate: number | null, lowerPercents: number[] | null): Promise<HeartRateZonesDto> =>
    (await apiClient.put<HeartRateZonesDto>('/cardio-tracks/heart-rate-zones', { maxHeartRate, lowerPercents })).data,

  recordBenchmark: async (id: string, value: number, notes?: string): Promise<BenchmarkResultDto> =>
    (await apiClient.post<BenchmarkResultDto>(`/cardio-tracks/${id}/benchmarks`, { value, notes: notes ?? null })).data,
};
