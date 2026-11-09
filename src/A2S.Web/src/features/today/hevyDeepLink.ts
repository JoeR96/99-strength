/**
 * Opening a pushed routine in the Hevy app. The `hevy://` scheme hands over to the app when it is
 * installed; when nothing picks it up the page is still visible 800 ms later and the web routine
 * opens instead. Leaving for Hevy is remembered so the next return to the tab triggers a pull.
 */

export const HEVY_WEB_FALLBACK_MS = 800;

export function hevyRoutineAppUrl(routineId: string): string {
  return 'hevy://routine/' + encodeURIComponent(routineId);
}

export function hevyRoutineWebUrl(routineId: string): string {
  return 'https://hevy.com/routine/' + encodeURIComponent(routineId);
}

let returnPending = false;

/** True once per "Open in Hevy": the tab coming back into view after it should pull. */
export function consumeHevyReturn(): boolean {
  const pending = returnPending;
  returnPending = false;
  return pending;
}

export interface OpenHevyRoutineOptions {
  /** Hands the deep link to the browser; defaults to a same-tab navigation. */
  navigate?: (url: string) => void;
  /** Opens the web fallback; defaults to a new tab, falling back to same-tab when blocked. */
  openWeb?: (url: string) => void;
  isVisible?: () => boolean;
  fallbackMs?: number;
}

function defaultNavigate(url: string): void {
  window.location.href = url;
}

function defaultOpenWeb(url: string): void {
  // No `noopener` feature here: with it `window.open` always returns null, which would look like a
  // blocked popup and send this tab to hevy.com as well. The opener is cut by hand instead.
  const opened = window.open(url, '_blank');
  if (opened) {
    opened.opener = null;
  } else {
    window.location.assign(url);
  }
}

/** Opens the routine in Hevy, or on hevy.com when the app does not take over. Returns a cancel. */
export function openHevyRoutine(routineId: string, options: OpenHevyRoutineOptions = {}): () => void {
  const {
    navigate = defaultNavigate,
    openWeb = defaultOpenWeb,
    isVisible = () => document.visibilityState === 'visible',
    fallbackMs = HEVY_WEB_FALLBACK_MS,
  } = options;

  returnPending = true;

  const fallback = () => {
    document.removeEventListener('visibilitychange', onVisibilityChange);
    if (isVisible()) openWeb(hevyRoutineWebUrl(routineId));
  };
  const timer = window.setTimeout(fallback, fallbackMs);
  const cancel = () => {
    window.clearTimeout(timer);
    document.removeEventListener('visibilitychange', onVisibilityChange);
  };
  // Phones freeze timers in a backgrounded tab and run them on return. Once the page has been
  // hidden the app took over, so the fallback must not fire when the person comes back.
  function onVisibilityChange() {
    if (!isVisible()) cancel();
  }
  document.addEventListener('visibilitychange', onVisibilityChange);

  navigate(hevyRoutineAppUrl(routineId));
  return cancel;
}
