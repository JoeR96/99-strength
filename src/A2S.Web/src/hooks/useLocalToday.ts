/**
 * Today's London date as state that rolls over at local midnight without a reload.
 * Re-evaluated every 30 s and whenever the tab becomes visible again (a phone
 * returning from the lock screen). Queries keyed by this date refetch on change.
 */

import { useEffect, useState } from 'react';
import { todayLocal, type LocalDate } from '@/lib/localDate';

const CHECK_INTERVAL_MS = 30_000;

export function useLocalToday(): LocalDate {
  const [date, setDate] = useState<LocalDate>(() => todayLocal());

  useEffect(() => {
    const check = () => setDate((current) => {
      const now = todayLocal();
      return now === current ? current : now;
    });
    const timer = window.setInterval(check, CHECK_INTERVAL_MS);
    const onVisible = () => {
      if (document.visibilityState === 'visible') check();
    };
    document.addEventListener('visibilitychange', onVisible);
    window.addEventListener('focus', check);
    return () => {
      window.clearInterval(timer);
      document.removeEventListener('visibilitychange', onVisible);
      window.removeEventListener('focus', check);
    };
  }, []);

  return date;
}
