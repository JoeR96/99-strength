/** A cardio exercise's finished sessions, newest first: week, date and the session in one line ("7 min @ level 6 · Easy"). */

interface CardioSession {
  weekNumber: number;
  completedAt?: string | null;
  cardioSummary?: string | null;
}

export function CardioSessionList({ sessions }: { sessions: CardioSession[] }) {
  const rows = sessions.filter((s) => s.cardioSummary).slice().reverse();
  if (rows.length === 0) return <p className="text-sm text-muted-foreground">No sessions yet.</p>;
  return (
    <ul className="divide-y divide-border/50 text-sm" aria-label="Cardio sessions" data-testid="cardio-sessions">
      {rows.map((s, i) => (
        <li key={`${s.weekNumber}-${i}`} className="flex flex-wrap items-baseline justify-between gap-x-3 py-2">
          <span className="text-muted-foreground">
            W{s.weekNumber}
            {s.completedAt ? ` · ${new Date(s.completedAt).toLocaleDateString()}` : ''}
          </span>
          <span className="font-medium text-foreground">{s.cardioSummary}</span>
        </li>
      ))}
    </ul>
  );
}
