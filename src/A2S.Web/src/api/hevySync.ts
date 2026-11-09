import apiClient from './apiClient';
import type { LocalDate } from '@/lib/localDate';
import type {
  AssignHevyWorkoutRequest,
  AssignHevyWorkoutResult,
  HevyInboxItemDto,
  PullSummary,
  PushRoutineResult,
  PushSessionRequest,
  PushWeekResult,
} from '@/types/hevySync';

/**
 * Hevy as the logging source of truth, all server-side: push a scheduled session as a routine, pull
 * completed workouts (on open/refresh; the server also polls every 15 minutes) and work the inbox.
 * The browser never sees the Hevy key.
 */
export const hevySyncApi = {
  /** Pulls events since the last pull and completes matched sessions. `force` re-walks from the plan start. */
  pull: async (force = false): Promise<PullSummary> => {
    const response = await apiClient.post<PullSummary>('/hevy/pull', null, { params: force ? { force: true } : undefined });
    return response.data;
  },

  /** Pulled workouts no session fitted, newest first. */
  getInbox: async (): Promise<HevyInboxItemDto[]> => {
    const response = await apiClient.get<HevyInboxItemDto[]>('/hevy/inbox');
    return response.data;
  },

  /** Completes the given session from an inbox workout (or records it as an extra session). */
  assign: async (hevyWorkoutId: string, request: AssignHevyWorkoutRequest): Promise<AssignHevyWorkoutResult> => {
    const response = await apiClient.post<AssignHevyWorkoutResult>(
      `/hevy/inbox/${encodeURIComponent(hevyWorkoutId)}/assign`,
      request,
    );
    return response.data;
  },

  ignore: async (hevyWorkoutId: string): Promise<void> => {
    await apiClient.post(`/hevy/inbox/${encodeURIComponent(hevyWorkoutId)}/ignore`);
  },

  /** Pushes one scheduled session to Hevy as a routine; pushing again updates it. */
  pushSession: async (planId: string, request: PushSessionRequest): Promise<PushRoutineResult> => {
    const response = await apiClient.post<PushRoutineResult>(`/plans/${planId}/sessions/push`, request);
    return response.data;
  },

  /** Pushes every non-rest session of the Mon–Sun week containing `date` (today when omitted). */
  pushWeek: async (planId: string, date?: LocalDate): Promise<PushWeekResult> => {
    const response = await apiClient.post<PushWeekResult>(`/plans/${planId}/sessions/push-week`, null, {
      params: date ? { date } : undefined,
    });
    return response.data;
  },
};
