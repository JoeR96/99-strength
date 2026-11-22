/**
 * Create or edit a routine drill by drill. Drills reorder with up/down buttons (no drag needed,
 * WCAG 2.5.7). The API sets a routine's name and target minutes on create only, so those two are
 * read-only when editing; the drills can always be changed.
 */

import { useMemo, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { ArrowDown, ArrowUp, ChevronLeft, Plus, Trash2 } from 'lucide-react';
import toast from 'react-hot-toast';
import { Navbar } from '@/components/layout/Navbar';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { cn } from '@/lib/utils';
import { useCreateMobilityRoutine, useMobilityRoutine, useReplaceDrills } from '@/hooks/useMobilityRoutines';
import { DoseUnit, SideBias, type MobilityRoutineDto } from '@/types/mobility';
import {
  draftFromRoutine,
  emptyDrill,
  emptyRoutine,
  hasErrors,
  moveDrill,
  toDrillDtos,
  validateRoutine,
  type DrillDraft,
  type RoutineDraft,
  type RoutineErrors,
} from './routineForm';
import { estimateMinutes } from './routinePlayer';

const selectClass =
  'flex h-11 w-full rounded-lg border border-border/50 bg-muted/30 px-3 text-base text-foreground focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20';

function FieldError({ id, message }: { id: string; message?: string }) {
  if (!message) return null;
  return (
    <p id={id} className="mt-1 text-sm text-destructive">
      {message}
    </p>
  );
}

export function RoutineEditorPage() {
  const { id } = useParams<{ id: string }>();
  const { routine, isLoading } = useMobilityRoutine(id);

  if (id !== undefined && isLoading) return <p className="p-4 text-muted-foreground">Loading…</p>;
  if (id !== undefined && !routine) {
    return (
      <div className="space-y-3 p-4">
        <p className="text-foreground">That routine doesn't exist.</p>
        <Link className="text-primary underline" to="/routines">
          Back to routines
        </Link>
      </div>
    );
  }
  return <RoutineForm key={routine?.id ?? 'new'} routine={routine} />;
}

function RoutineForm({ routine }: { routine: MobilityRoutineDto | undefined }) {
  const id = routine?.id;
  const editing = routine !== undefined;
  const navigate = useNavigate();
  const create = useCreateMobilityRoutine();
  const replace = useReplaceDrills();

  const [draft, setDraft] = useState<RoutineDraft>(() => (routine ? draftFromRoutine(routine) : emptyRoutine()));
  const [submitted, setSubmitted] = useState(false);

  const errors: RoutineErrors = useMemo(() => validateRoutine(draft), [draft]);
  const shown = submitted ? errors : { drill: {} };
  const saving = create.isPending || replace.isPending;
  const estimate = useMemo(
    () => (hasErrors({ ...errors, name: undefined, targetMinutes: undefined }) ? null : estimateMinutes({ id: '', name: '', targetMinutes: 1, drills: toDrillDtos(draft) })),
    [draft, errors]
  );

  const updateDrill = (key: string, patch: Partial<DrillDraft>) =>
    setDraft((d) => ({ ...d, drills: d.drills.map((x) => (x.key === key ? { ...x, ...patch } : x)) }));

  const save = () => {
    setSubmitted(true);
    if (hasErrors(errors)) {
      toast.error('Fix the highlighted fields first');
      return;
    }
    const drills = toDrillDtos(draft);
    const onSuccess = () => {
      toast.success(editing ? 'Routine saved' : 'Routine created');
      navigate('/routines');
    };
    const onError = () => toast.error("Couldn't save the routine. Try again.");
    if (editing && id) replace.mutate({ id, drills }, { onSuccess, onError });
    else create.mutate({ name: draft.name.trim(), targetMinutes: Number(draft.targetMinutes), drills }, { onSuccess, onError });
  };

  return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <main className="container-page space-y-4 py-4 pb-28">
        <Link to="/routines" className="inline-flex min-h-11 items-center gap-1 text-sm text-muted-foreground hover:text-foreground">
          <ChevronLeft className="h-4 w-4" aria-hidden="true" />
          Routines
        </Link>
        <h1 className="text-2xl font-bold text-foreground">{editing ? `Edit ${routine?.name}` : 'New routine'}</h1>

        <section className="space-y-3 rounded-lg border border-border bg-card p-4">
          <div>
            <label htmlFor="routine-name" className="mb-1 block text-sm font-medium text-foreground">
              Name
            </label>
            <Input
              id="routine-name"
              value={draft.name}
              readOnly={editing}
              aria-invalid={!!shown.name}
              aria-describedby={shown.name ? 'routine-name-error' : undefined}
              onChange={(e) => setDraft((d) => ({ ...d, name: e.target.value }))}
              placeholder="Evening reset"
            />
            <FieldError id="routine-name-error" message={shown.name} />
          </div>
          <div>
            <label htmlFor="routine-minutes" className="mb-1 block text-sm font-medium text-foreground">
              Target minutes
            </label>
            <Input
              id="routine-minutes"
              inputMode="numeric"
              value={draft.targetMinutes}
              readOnly={editing}
              aria-invalid={!!shown.targetMinutes}
              aria-describedby={shown.targetMinutes ? 'routine-minutes-error' : 'routine-minutes-hint'}
              onChange={(e) => setDraft((d) => ({ ...d, targetMinutes: e.target.value }))}
            />
            <FieldError id="routine-minutes-error" message={shown.targetMinutes} />
            {!shown.targetMinutes && (
              <p id="routine-minutes-hint" className="mt-1 text-sm text-muted-foreground">
                {estimate !== null ? `The drills below take about ${estimate} min.` : 'Fill in the drills to see how long it takes.'}
                {editing && ' Name and target minutes are set when the routine is created.'}
              </p>
            )}
          </div>
        </section>

        <section aria-labelledby="drills-heading" className="space-y-3">
          <h2 id="drills-heading" className="text-lg font-semibold text-foreground">
            Drills
          </h2>
          {shown.drills && <p className="text-sm text-destructive">{shown.drills}</p>}

          <ol className="space-y-3">
            {draft.drills.map((drill, index) => {
              const e = shown.drill[drill.key] ?? {};
              const fid = (f: string) => `${drill.key}-${f}`;
              return (
                <li key={drill.key} className="space-y-3 rounded-lg border border-border bg-card p-4" aria-label={`Drill ${index + 1}${drill.name ? `: ${drill.name}` : ''}`}>
                  <div className="flex items-center justify-between gap-2">
                    <span className="text-sm font-medium text-muted-foreground">Drill {index + 1}</span>
                    <div className="flex gap-1">
                      <Button type="button" variant="ghost" size="icon" className="h-11 w-11" disabled={index === 0} aria-label={`Move drill ${index + 1} up`} onClick={() => setDraft((d) => ({ ...d, drills: moveDrill(d.drills, index, -1) }))}>
                        <ArrowUp aria-hidden="true" />
                      </Button>
                      <Button type="button" variant="ghost" size="icon" className="h-11 w-11" disabled={index === draft.drills.length - 1} aria-label={`Move drill ${index + 1} down`} onClick={() => setDraft((d) => ({ ...d, drills: moveDrill(d.drills, index, 1) }))}>
                        <ArrowDown aria-hidden="true" />
                      </Button>
                      <Button type="button" variant="ghost" size="icon" className="h-11 w-11" aria-label={`Remove drill ${index + 1}`} onClick={() => setDraft((d) => ({ ...d, drills: d.drills.filter((x) => x.key !== drill.key) }))}>
                        <Trash2 aria-hidden="true" />
                      </Button>
                    </div>
                  </div>

                  <div>
                    <label htmlFor={fid('name')} className="mb-1 block text-sm font-medium text-foreground">
                      Drill name
                    </label>
                    <Input id={fid('name')} value={drill.name} aria-invalid={!!e.name} aria-describedby={e.name ? fid('name-error') : undefined} onChange={(ev) => updateDrill(drill.key, { name: ev.target.value })} placeholder="Half-kneeling hip flexor stretch" />
                    <FieldError id={fid('name-error')} message={e.name} />
                  </div>

                  <div className="grid grid-cols-3 gap-2">
                    <div>
                      <label htmlFor={fid('sets')} className="mb-1 block text-sm font-medium text-foreground">
                        Sets
                      </label>
                      <Input id={fid('sets')} inputMode="numeric" value={drill.sets} aria-invalid={!!e.sets} aria-describedby={e.sets ? fid('sets-error') : undefined} onChange={(ev) => updateDrill(drill.key, { sets: ev.target.value })} />
                      <FieldError id={fid('sets-error')} message={e.sets} />
                    </div>
                    <div>
                      <label htmlFor={fid('amount')} className="mb-1 block text-sm font-medium text-foreground">
                        Amount
                      </label>
                      <Input id={fid('amount')} inputMode="numeric" value={drill.amount} aria-invalid={!!e.amount} aria-describedby={e.amount ? fid('amount-error') : undefined} onChange={(ev) => updateDrill(drill.key, { amount: ev.target.value })} />
                      <FieldError id={fid('amount-error')} message={e.amount} />
                    </div>
                    <div>
                      <label htmlFor={fid('unit')} className="mb-1 block text-sm font-medium text-foreground">
                        Unit
                      </label>
                      <select id={fid('unit')} className={selectClass} value={drill.unit} onChange={(ev) => updateDrill(drill.key, { unit: Number(ev.target.value) as DoseUnit })}>
                        <option value={DoseUnit.Seconds}>seconds</option>
                        <option value={DoseUnit.Breaths}>breaths</option>
                        <option value={DoseUnit.Reps}>reps</option>
                      </select>
                    </div>
                  </div>

                  <div className="grid grid-cols-2 gap-2">
                    <label htmlFor={fid('perside')} className={cn('flex min-h-11 items-center gap-2 rounded-lg border px-3 text-sm', drill.perSide ? 'border-primary/50 text-foreground' : 'border-border text-muted-foreground')}>
                      <input id={fid('perside')} type="checkbox" className="h-5 w-5 accent-[hsl(25_80%_50%)]" checked={drill.perSide} onChange={(ev) => updateDrill(drill.key, { perSide: ev.target.checked })} />
                      Each side
                    </label>
                    <div>
                      <label htmlFor={fid('bias')} className="sr-only">
                        Extra set
                      </label>
                      <select id={fid('bias')} className={selectClass} value={drill.extraSetSide} onChange={(ev) => updateDrill(drill.key, { extraSetSide: Number(ev.target.value) as SideBias })}>
                        <option value={SideBias.None}>No extra set</option>
                        <option value={SideBias.Left}>Extra set left</option>
                        <option value={SideBias.Right}>Extra set right</option>
                      </select>
                    </div>
                  </div>

                  <div>
                    <label htmlFor={fid('cues')} className="mb-1 block text-sm font-medium text-foreground">
                      Cues <span className="font-normal text-muted-foreground">(optional)</span>
                    </label>
                    <Input id={fid('cues')} value={drill.cues} onChange={(ev) => updateDrill(drill.key, { cues: ev.target.value })} placeholder="Squeeze the back glute, ribs down" />
                  </div>
                  <div>
                    <label htmlFor={fid('alt')} className="mb-1 block text-sm font-medium text-foreground">
                      Standing alternative <span className="font-normal text-muted-foreground">(optional)</span>
                    </label>
                    <Input id={fid('alt')} value={drill.standingAlternative} onChange={(ev) => updateDrill(drill.key, { standingAlternative: ev.target.value })} placeholder="For when the floor is out" />
                  </div>
                </li>
              );
            })}
          </ol>

          <Button type="button" variant="outline" className="h-12 w-full" onClick={() => setDraft((d) => ({ ...d, drills: [...d.drills, emptyDrill()] }))} data-testid="add-drill">
            <Plus aria-hidden="true" />
            Add drill
          </Button>
        </section>

        <div className="sticky bottom-[calc(3.5rem+env(safe-area-inset-bottom))] lg:bottom-4">
          <Button type="button" size="lg" className="h-12 w-full" onClick={save} disabled={saving} data-testid="save-routine">
            {saving ? 'Saving…' : editing ? 'Save changes' : 'Create routine'}
          </Button>
        </div>
      </main>
    </div>
  );
}
