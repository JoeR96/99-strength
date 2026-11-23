import apiClient from './apiClient';

export interface FinisherNudgeRequest {
  at: string;
  body: string;
}

/** `/api/v1/finisher-sessions/{clientSessionId}/schedule`: the live finisher wizard's phase-change notifications. */
export const finisherSessionsApi = {
  /** Replaces the whole schedule (an empty list: nothing to announce, e.g. while paused). */
  replace: async (clientSessionId: string, request: { day: number; title?: string | null; nudges: FinisherNudgeRequest[] }): Promise<void> => {
    await apiClient.post(`/finisher-sessions/${encodeURIComponent(clientSessionId)}/schedule`, request);
  },

  remove: async (clientSessionId: string): Promise<void> => {
    await apiClient.delete(`/finisher-sessions/${encodeURIComponent(clientSessionId)}/schedule`);
  },
};
