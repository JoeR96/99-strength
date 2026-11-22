import apiClient from './apiClient';
import type { ScheduleWarning, SessionKind, StartSuggestedPlanResult, SuggestedPlanPreview } from './suggestedPlans';

/**
 * Typed client for plan templates (`GET /plans/templates`, `GET /plans/templates/{key}`,
 * `POST /plans/templates/{key}/apply`). A template preview is a suggested plan preview plus the
 * phases, sessions, weekly slots, routines, cardio tracks and what is left to fill in.
 */

export interface PlanTemplatePhase {
  type: string;
  /** yyyy-MM-dd */
  from: string;
  to: string;
  note: string | null;
}

export interface PlanTemplateExercise {
  /** "1", "2a", "2b" … */
  slot: string;
  name: string;
  prescription: string;
  progression: string;
  note: string | null;
}

export interface PlanTemplateSession {
  label: string;
  liftDay: number;
  warmUp: string | null;
  finisher: string | null;
  estimatedMinutes: number;
  exercises: PlanTemplateExercise[];
}

export interface PlanTemplateSlot {
  /** 0 = Sunday … 6 = Saturday. */
  day: number;
  /** The server sends SessionKind as a number (0 Rest, 1 Lift, 2 Cardio, 3 Mobility); stories use names. */
  kind: SessionKind | number;
  label: string;
  warmUp: string | null;
  finisher: string | null;
  routine: string | null;
  track: string | null;
}

export interface PlanTemplateRoutine {
  name: string;
  targetMinutes: number;
  drills: { name: string; dose: string; cues: string | null }[];
}

export interface PlanTemplatePreview extends SuggestedPlanPreview {
  phases: PlanTemplatePhase[];
  sessions: PlanTemplateSession[];
  slots: PlanTemplateSlot[];
  routines: PlanTemplateRoutine[];
  cardioTracks: { key?: string; name: string; description: string }[];
  toFillIn: string[];
  blockSequence: number[];
  programWeeks: number;
}

export interface ApplyPlanTemplateRequest {
  /** yyyy-MM-dd; the server snaps it to a Monday. */
  startDate?: string;
  /** Input key → value; null leaves a training max pending. */
  trainingMaxes: Record<string, number | null>;
  startWeightKg?: number;
  confirmReplace: boolean;
}

export type ApplyPlanTemplateResult = StartSuggestedPlanResult;
export type { ScheduleWarning };

/** The 409 body when another plan is active and `confirmReplace` was false. */
export interface PlanReplaceProblem {
  title?: string;
  detail?: string;
  code?: string;
  activePlanName?: string;
}

/** The active plan's name when the error is "confirm to replace", else null. */
export function replaceConfirmation(error: unknown): { activePlanName: string } | null {
  const response = (error as { response?: { status?: number; data?: PlanReplaceProblem } })?.response;
  if (response?.status !== 409) return null;
  const data = response.data ?? {};
  if (data.code && data.code !== 'PlanReplaceNeedsConfirmation') return null;
  // The server's detail: "You already have an active plan (Cut to 85 — Nov 2026). Confirm to replace it: …"
  const fromDetail = /active plan \((.+?)\)\./.exec(data.detail ?? '')?.[1];
  return { activePlanName: data.activePlanName ?? fromDetail ?? 'your current plan' };
}

const KIND_NAMES: SessionKind[] = ['Rest', 'Lift', 'Cardio', 'Mobility'];

/** A slot's kind as a name, whether it came as a number or a string. */
export function kindName(kind: SessionKind | number): SessionKind {
  return typeof kind === 'number' ? (KIND_NAMES[kind] ?? 'Rest') : kind;
}

const query = (startDate?: string) => (startDate ? { params: { startDate } } : undefined);

export const planTemplatesApi = {
  list: async (startDate?: string): Promise<PlanTemplatePreview[]> => {
    const response = await apiClient.get<PlanTemplatePreview[]>('/plans/templates', query(startDate));
    return response.data;
  },

  get: async (key: string, startDate?: string): Promise<PlanTemplatePreview> => {
    const response = await apiClient.get<PlanTemplatePreview>(`/plans/templates/${encodeURIComponent(key)}`, query(startDate));
    return response.data;
  },

  apply: async (key: string, request: ApplyPlanTemplateRequest): Promise<ApplyPlanTemplateResult> => {
    const response = await apiClient.post<ApplyPlanTemplateResult>(`/plans/templates/${encodeURIComponent(key)}/apply`, request);
    return response.data;
  },
};

export default planTemplatesApi;
