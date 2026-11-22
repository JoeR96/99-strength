import { useActivePlan } from '@/hooks/usePlan';
import { isoWeekday, type LocalDate } from '@/lib/localDate';
import type { ScheduledSessionDto, TodaySessionDto } from '@/types/plan';

/** The weekly slot behind a session on `date` (the schedule uses .NET's DayOfWeek, Sunday = 0). */
export function useScheduledSlot(
  session: TodaySessionDto,
  date: LocalDate
): ScheduledSessionDto | null {
  const { data: plan } = useActivePlan();
  const weekday = (isoWeekday(date) + 1) % 7;
  return (
    plan?.schedule.find(
      (s) => s.day === weekday && s.kind === session.kind && s.label === session.label
    ) ?? null
  );
}
