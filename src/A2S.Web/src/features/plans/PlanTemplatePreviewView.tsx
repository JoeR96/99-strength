/**
 * Everything a plan template sets up, before applying it: phases with dates from the chosen start,
 * the weekly schedule, each session's exercises (slot, prescription, progression), warm-ups and
 * finishers, home routines with their drills, cardio tracks and what is left to fill in.
 */

import { kindName, type PlanTemplatePreview } from '@/api/planTemplates';
import { formatShortDate } from './planTemplateForm';

const DAY = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
/** Monday first. */
const WEEK_ORDER = [1, 2, 3, 4, 5, 6, 0];

const CARD = 'rounded-lg border border-border bg-card p-4';

function Section({ id, title, children }: { id: string; title: string; children: React.ReactNode }) {
  return (
    <section className={CARD} aria-labelledby={id}>
      <h2 id={id} className="mb-3 text-lg font-semibold text-foreground">
        {title}
      </h2>
      {children}
    </section>
  );
}

export function PlanTemplatePreviewView({ template }: { template: PlanTemplatePreview }) {
  const slotsByDay = WEEK_ORDER.map((day) => ({ day, slots: template.slots.filter((s) => s.day === day) })).filter((d) => d.slots.length > 0);

  return (
    <div className="space-y-4" data-testid="plan-template-preview">
      <p className="text-body text-foreground">{template.description}</p>
      <p className="text-sm text-muted-foreground">
        Starts {formatShortDate(template.startDate)} · A2S2 blocks {template.blockSequence.join(', ')} ({template.programWeeks} weeks)
      </p>

      {template.phases.length > 0 && (
        <Section id="tpl-phases" title="Phases">
          <ol className="space-y-2">
            {template.phases.map((p, i) => (
              <li key={`${p.from}-${i}`} className="flex flex-wrap justify-between gap-x-3 text-sm">
                <span className="font-medium text-foreground">
                  {p.note ?? p.type}
                  {p.note && <span className="text-muted-foreground"> · {p.type}</span>}
                </span>
                <span className="text-muted-foreground">
                  {formatShortDate(p.from)} – {formatShortDate(p.to)}
                </span>
              </li>
            ))}
          </ol>
        </Section>
      )}

      {slotsByDay.length > 0 && (
        <Section id="tpl-week" title="Your week">
          <ul className="space-y-3">
            {slotsByDay.map(({ day, slots }) => (
              <li key={day}>
                <p className="font-medium text-foreground">{DAY[day]}</p>
                <ul className="mt-1 space-y-1">
                  {slots.map((s, i) => (
                    <li key={`${s.label}-${i}`} className="text-sm text-muted-foreground">
                      <span className="text-foreground">{s.routine && kindName(s.kind) === 'Mobility' ? s.routine : s.label}</span>
                      {kindName(s.kind) !== 'Rest' && ` · ${kindName(s.kind)}`}
                      {s.track && ` · ${s.track}`}
                      {s.warmUp && ` · warm-up: ${s.warmUp}`}
                      {s.finisher && ` · finisher: ${s.finisher}`}
                    </li>
                  ))}
                </ul>
              </li>
            ))}
          </ul>
        </Section>
      )}

      {template.sessions.map((session) => (
        <Section key={session.label} id={`tpl-session-${session.liftDay}`} title={`${session.label} · about ${session.estimatedMinutes} min`}>
          {session.warmUp && <p className="mb-2 text-sm text-muted-foreground">Warm-up: {session.warmUp}</p>}
          <ol className="space-y-2">
            {session.exercises.map((e) => (
              <li key={`${e.slot}-${e.name}`} className="flex gap-3 rounded-md bg-muted/40 px-3 py-2">
                <span className="w-8 shrink-0 font-semibold text-foreground">{e.slot}</span>
                <span className="min-w-0 flex-1">
                  <span className="block font-medium text-foreground">{e.name}</span>
                  <span className="block text-sm text-muted-foreground">
                    {e.prescription} · {e.progression}
                    {e.note && ` · ${e.note}`}
                  </span>
                </span>
              </li>
            ))}
          </ol>
          {session.finisher && <p className="mt-2 text-sm text-muted-foreground">Finisher: {session.finisher}</p>}
        </Section>
      ))}

      {template.routines.length > 0 && (
        <Section id="tpl-routines" title="Home routines">
          <ul className="space-y-3">
            {template.routines.map((r) => (
              <li key={r.name}>
                <p className="font-medium text-foreground">
                  {r.name} <span className="font-normal text-muted-foreground">· {r.targetMinutes} min</span>
                </p>
                <ul className="mt-1 space-y-1">
                  {r.drills.map((d) => (
                    <li key={d.name} className="text-sm text-muted-foreground">
                      <span className="text-foreground">{d.name}</span> · {d.dose}
                      {d.cues && ` · ${d.cues}`}
                    </li>
                  ))}
                </ul>
              </li>
            ))}
          </ul>
        </Section>
      )}

      {template.cardioTracks.length > 0 && (
        <Section id="tpl-cardio" title="Cardio tracks">
          <ul className="space-y-2">
            {template.cardioTracks.map((t) => (
              <li key={t.name} className="text-sm">
                <span className="font-medium text-foreground">{t.name}</span>
                <span className="block text-muted-foreground">{t.description}</span>
              </li>
            ))}
          </ul>
        </Section>
      )}

      {template.toFillIn.length > 0 && (
        <Section id="tpl-fill" title="What's left to fill in">
          <ul className="list-disc space-y-1 pl-5 text-sm text-foreground">
            {template.toFillIn.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        </Section>
      )}
    </div>
  );
}
