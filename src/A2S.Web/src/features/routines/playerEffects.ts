/** Browser side effects for the routine player: a short beep and keeping the screen awake. Both fail quietly. */

import { useEffect } from 'react';

let audio: AudioContext | null = null;

/** A short tone: higher for "go", lower for "stop/switch". Silent where Web Audio is unavailable. */
export function beep(kind: 'go' | 'switch' | 'finish' = 'go'): void {
  try {
    const Ctx = window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
    if (!Ctx) return;
    audio ??= new Ctx();
    const osc = audio.createOscillator();
    const gain = audio.createGain();
    osc.frequency.value = kind === 'go' ? 880 : kind === 'switch' ? 660 : 520;
    gain.gain.setValueAtTime(0.15, audio.currentTime);
    gain.gain.exponentialRampToValueAtTime(0.0001, audio.currentTime + (kind === 'finish' ? 0.6 : 0.2));
    osc.connect(gain).connect(audio.destination);
    osc.start();
    osc.stop(audio.currentTime + (kind === 'finish' ? 0.6 : 0.2));
  } catch {
    /* sound is a nicety */
  }
}

/** Unlocks audio on iOS: call from a tap handler before the first beep. */
export function primeAudio(): void {
  try {
    const Ctx = window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
    if (!Ctx) return;
    audio ??= new Ctx();
    void audio.resume();
  } catch {
    /* ignore */
  }
}

interface WakeLockSentinelLike {
  release: () => Promise<void>;
}

/** Holds a screen wake lock while `active`, re-taking it when the app comes back to the foreground. */
export function useWakeLock(active: boolean): void {
  useEffect(() => {
    if (!active) return;
    const nav = navigator as Navigator & { wakeLock?: { request: (type: 'screen') => Promise<WakeLockSentinelLike> } };
    if (!nav.wakeLock) return;
    let sentinel: WakeLockSentinelLike | null = null;
    let cancelled = false;
    const acquire = () => {
      nav.wakeLock!
        .request('screen')
        .then((s) => {
          if (cancelled) void s.release();
          else sentinel = s;
        })
        .catch(() => undefined);
    };
    const onVisible = () => {
      if (document.visibilityState === 'visible') acquire();
    };
    acquire();
    document.addEventListener('visibilitychange', onVisible);
    return () => {
      cancelled = true;
      document.removeEventListener('visibilitychange', onVisible);
      void sentinel?.release().catch(() => undefined);
    };
  }, [active]);
}
