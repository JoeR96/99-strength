/** Where the track info sheet keeps its unsaved draft (see useFormDraft). */

import { clearFormDraft } from '@/hooks/useFormDraft';

/** Bump when the sheet's form changes shape, so an old draft is dropped instead of restored. */
export const TRACK_DRAFT_VERSION = 2; // 2: reset-to and warm-up / cool-down fields
export const trackDraftKey = (trackId: string) => `cardio-track:${trackId}`;

/** Call after the track saved: the sheet's unsaved draft is no longer needed. */
export function clearTrackInfoDraft(trackId: string): void {
  clearFormDraft(trackDraftKey(trackId));
}
