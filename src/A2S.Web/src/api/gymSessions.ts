import apiClient from './apiClient';
import { idempotent } from './idempotency';
import type { CardioProtocolDto, FinishGymSessionRequest, FinishGymSessionResult, HevyPostStatusDto } from '@/types/gym';

/** `/api/v1/gym-sessions`: finish a gym-mode session (completes it and posts it to Hevy from the server). */
export const gymSessionsApi = {
  /** Idempotent on `clientSessionId`: a replay returns the first result with `replayed: true`. */
  finish: async (request: FinishGymSessionRequest, idempotencyKey?: string): Promise<FinishGymSessionResult> => {
    const response = await apiClient.post<FinishGymSessionResult>('/gym-sessions', request, idempotent(idempotencyKey));
    return response.data;
  },

  /** "Not in Hevy yet" → Retry. */
  retryHevy: async (clientSessionId: string): Promise<HevyPostStatusDto> => {
    const response = await apiClient.post<HevyPostStatusDto>(`/gym-sessions/${clientSessionId}/hevy-retry`);
    return response.data;
  },
};

/** `/api/v1/cardio-protocols`: the user's cardio protocols (finisher prescriptions). */
export const cardioProtocolsApi = {
  list: async (): Promise<CardioProtocolDto[]> => {
    const response = await apiClient.get<CardioProtocolDto[]>('/cardio-protocols');
    return response.data;
  },
};
