/**
 * One plain line about an upcoming session, for the Up next card and the preview sheet:
 * a lift's exercise count and main lift with its top set (the server's lift block on `/plans/upcoming`,
 * from the current A2S2 week since weeks advance on completion, not by date), a cardio track's prescription or protocol's minutes, or a
 * mobility routine's name and minutes. Renders nothing until the data is there.
 */

import { useActivePlan } from '@/hooks/usePlan';
import { useCardioTracks } from '@/hooks/useCardioTracks';
import { useCardioProtocols } from '@/hooks/useGymSession';
import { useMobilityRoutines } from '@/hooks/useMobilityRoutines';
import { isoWeekday, type LocalDate } from '@/lib/localDate';
import { SessionKind, type ScheduledSessionDto, type TodaySessionDto, type UpcomingLiftDto } from '@/types/plan';
import { liftSummary } from './upNext';

/** The weekly slot behind a session on `date` (the schedule uses .NET's DayOfWeek, Sunday = 0). */
export function useScheduledSlot(session: TodaySessionDto, date: LocalDate): ScheduledSessionDto | null {
  const { data: plan } = useActivePlan();
  const weekday = (isoWeekday(date) + 1) % 7;
  return plan?.schedule.find((s) => s.day === weekday && s.kind === session.kind && s.label === session.label) ?? null;
}

function LiftSummary({ lift }: { lift: UpcomingLiftDto }) {
  const text = liftSummary(lift);
  return text ? <Line text={text} /> : null;
}

function CardioSummary({ session, date }: { session: TodaySessionDto; date: LocalDate }) {
  const slot = useScheduledSlot(session, date);
  const trackId = slot?.cardioTrackId ?? null;
  const tracks = useCardioTracks(Boolean(trackId));
  const protocols = useCardioProtocols(!trackId && Boolean(session.cardioProtocolId));
  const track = trackId ? tracks.data?.find((t) => t.id === trackId) : null;
  if (track) return <Line text={track.prescription.title} />;
  const protocol = protocols.data?.find((p) => p.id === session.cardioProtocolId);
  if (!protocol) return null;
  return <Line text={protocol.notes ? `${protocol.totalMinutes} min · ${protocol.notes}` : `${protocol.totalMinutes} min`} />;
}

function MobilitySummary({ routineId }: { routineId: string }) {
  const routines = useMobilityRoutines();
  const routine = routines.data?.find((r) => r.id === routineId);
  return routine ? <Line text={`${routine.name} · ${routine.targetMinutes} min`} /> : null;
}

function Line({ text }: { text: string }) {
  return (
    <p className="truncate text-sm text-muted-foreground" data-testid="session-summary">
      {text}
    </p>
  );
}

export function SessionSummary({ session, date, lifts = [] }: { session: TodaySessionDto; date: LocalDate; lifts?: UpcomingLiftDto[] }) {
  if (session.kind === SessionKind.Lift) {
    const lift = lifts.find((l) => l.liftDay === session.liftDay && l.label === session.label);
    return lift ? <LiftSummary lift={lift} /> : null;
  }
  if (session.kind === SessionKind.Cardio) return <CardioSummary session={session} date={date} />;
  if (session.kind === SessionKind.Mobility && session.mobilityRoutineId) return <MobilitySummary routineId={session.mobilityRoutineId} />;
  return null;
}
