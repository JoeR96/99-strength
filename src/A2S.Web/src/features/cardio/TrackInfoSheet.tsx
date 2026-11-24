/**
 * A track's info sheet: what each stage does, the one-line safety note, and the editable
 * parameters (name, notes, sessions per step, ranges, steps, where volume resets to after a level, and the easy\n * warm-up / cool-down minutes and level; 0 minutes turns one off). Saving re-evaluates nothing;
 * the track keeps its place inside the new ranges and the edit can be undone.
 */

import { useState, type FormEvent } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { IntensityUnit, intensityLabel, type CardioTrackDto, type TrackSegmentDto, type TrackStageDto, type UpdateCardioTrackRequest } from '@/types/cardioTracks';
import { describeStage } from './trackText';
import { optionalNumber } from './TrackLogSheet';
import { useFormDraft } from '@/hooks/useFormDraft';
import { TRACK_DRAFT_VERSION, trackDraftKey } from './trackInfoDraft';
import { DraftRestorePrompt } from '@/components/shared/DraftRestorePrompt';
import { UnsavedChangesGuard } from '@/components/shared/UnsavedChangesGuard';


interface TrackInfoForm {
  name: string;
  notes: string;
  stages: StageForm[];
}

export interface TrackInfoSheetProps {
  track: CardioTrackDto | null;
  onClose: () => void;
  onSave: (track: CardioTrackDto, request: UpdateCardioTrackRequest) => void;
  saving?: boolean;
}

interface StageForm {
  sessions: string;
  min: string;
  max: string;
  step: string;
  intensityStart: string;
  intensityStep: string;
  intensityMax: string;
  /** Where volume goes back to after an intensity step ('' = the bottom of the range). */
  reset: string;
  /** Warm-up / cool-down minutes ('0' or '' = off) and easy level. */
  warmMinutes: string;
  warmLevel: string;
  coolMinutes: string;
  coolLevel: string;
}

const DEFAULT_WARM_ZONE = 'Z2';
const DEFAULT_COOL_ZONE = 'Z1–2';

function toForm(stage: TrackStageDto): StageForm {
  const d = stage.doubleProgression;
  return {
    sessions: String(stage.weekProgram?.sessionsToAdvance ?? d?.sessionsToAdvance ?? 2),
    min: d ? String(d.volumeMin) : '',
    max: d ? String(d.volumeMax) : '',
    step: d ? String(d.volumeStepPercent ?? d.volumeStep) : '',
    reset: d?.volumeResetTo?.toString() ?? '',
    warmMinutes: String(stage.warmUp?.minutes ?? 0),
    warmLevel: stage.warmUp?.level?.toString() ?? '',
    coolMinutes: String(stage.coolDown?.minutes ?? 0),
    coolLevel: stage.coolDown?.level?.toString() ?? '',
    intensityStart: d?.intensityStart?.toString() ?? '',
    intensityStep: d?.intensityStep?.toString() ?? '',
    intensityMax: d?.intensityMax?.toString() ?? '',
  };
}

/** A warm-up or cool-down from the form: 0 or blank minutes turns it off; the zone note is kept. */
export function segmentFromForm(minutes: string, level: string, existing: TrackSegmentDto | null | undefined, zone: string): TrackSegmentDto | null {
  const m = Math.round(optionalNumber(minutes) ?? 0);
  if (m <= 0) return null;
  return { minutes: Math.min(30, m), level: optionalNumber(level), zone: existing?.zone ?? zone };
}

export function fromForm(stage: TrackStageDto, form: StageForm): TrackStageDto {
  const sessions = Math.max(1, Math.round(optionalNumber(form.sessions) ?? 2));
  const segments = {
    warmUp: segmentFromForm(form.warmMinutes ?? '0', form.warmLevel ?? '', stage.warmUp, DEFAULT_WARM_ZONE),
    coolDown: segmentFromForm(form.coolMinutes ?? '0', form.coolLevel ?? '', stage.coolDown, DEFAULT_COOL_ZONE),
  };
  if (stage.weekProgram) {
    return { ...stage, ...segments, weekProgram: { ...stage.weekProgram, sessionsToAdvance: sessions } };
  }
  const d = stage.doubleProgression!;
  const step = optionalNumber(form.step) ?? d.volumeStep;
  const volumeMin = optionalNumber(form.min) ?? d.volumeMin;
  const volumeMax = optionalNumber(form.max) ?? d.volumeMax;
  const reset = optionalNumber(form.reset ?? '');
  return {
    ...stage,
    ...segments,
    doubleProgression: {
      ...d,
      sessionsToAdvance: sessions,
      volumeMin,
      volumeMax,
      // Outside the range (or blank) means "back to the bottom", the original rule.
      volumeResetTo: reset !== null && reset >= volumeMin && reset <= volumeMax ? reset : null,
      volumeStep: d.volumeStepPercent ? d.volumeStep : step,
      volumeStepPercent: d.volumeStepPercent ? step : null,
      intensityStart: d.intensityUnit === IntensityUnit.None ? null : optionalNumber(form.intensityStart) ?? d.intensityStart,
      intensityStep: d.intensityUnit === IntensityUnit.None ? null : optionalNumber(form.intensityStep) ?? d.intensityStep,
      intensityMax: d.intensityUnit === IntensityUnit.None ? null : optionalNumber(form.intensityMax),
    },
  };
}

function NumberField({ id, label, value, onChange }: { id: string; label: string; value: string; onChange: (v: string) => void }) {
  return (
    <div>
      <Label htmlFor={id}>{label}</Label>
      <Input id={id} inputMode="decimal" value={value} onChange={(e) => onChange(e.target.value)} className="mt-1 h-12" />
    </div>
  );
}

export function TrackInfoSheet({ track, onClose, onSave, saving = false }: TrackInfoSheetProps) {
  const [initial] = useState<TrackInfoForm>(() => ({ name: track?.name ?? '', notes: track?.notes ?? '', stages: track?.stages.map(toForm) ?? [] }));
  const [name, setName] = useState(initial.name);
  const [notes, setNotes] = useState(initial.notes);
  const [forms, setForms] = useState<StageForm[]>(initial.stages);
  const unsaved = useFormDraft<TrackInfoForm>({
    key: trackDraftKey(track?.id ?? 'none'),
    version: TRACK_DRAFT_VERSION,
    value: { name, notes, stages: forms },
    initial,
  });
  const restore = () => {
    const draft = unsaved.restore();
    setName(draft.name);
    setNotes(draft.notes);
    setForms(draft.stages);
  };

  const setField = (index: number, field: keyof StageForm, value: string) =>
    setForms((current) => current.map((f, i) => (i === index ? { ...f, [field]: value } : f)));

  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (!track) return;
    onSave(track, {
      name: name.trim() || track.name,
      notes: notes.trim() === '' ? null : notes.trim(),
      stages: track.stages.map((stage, i) => fromForm(stage, forms[i] ?? toForm(stage))),
      benchmark: track.benchmark,
    });
  };

  return (
    <Dialog open={track !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="bottom-0 top-auto max-h-[92vh] translate-y-0 overflow-y-auto rounded-t-lg p-6 sm:bottom-auto sm:top-1/2 sm:-translate-y-1/2">
        <form onSubmit={submit} className="space-y-4" aria-label={`${track?.name ?? 'Track'} settings`}>
          <DialogHeader>
            <DialogTitle>{track?.name ?? 'Track'}</DialogTitle>
            <DialogDescription>{track?.safetyNote}</DialogDescription>
            <a href="/progression#tracks" target="_blank" rel="noreferrer" className="text-sm text-primary underline underline-offset-2">
              How do track stages progress?
            </a>
          </DialogHeader>
          <UnsavedChangesGuard when={unsaved.isDirty} />
          {unsaved.pendingDraft && <DraftRestorePrompt savedAt={unsaved.pendingDraft.savedAt} onRestore={restore} onDiscard={unsaved.discard} />}

          <div>
            <Label htmlFor="track-name">Name</Label>
            <Input id="track-name" value={name} onChange={(e) => setName(e.target.value)} className="mt-1 h-12" />
          </div>
          <div>
            <Label htmlFor="track-notes">Notes</Label>
            <Input id="track-notes" value={notes} onChange={(e) => setNotes(e.target.value)} className="mt-1 h-12" />
          </div>

          {track?.stages.map((stage, index) => {
            const form = forms[index] ?? toForm(stage);
            const d = stage.doubleProgression;
            return (
              <fieldset key={stage.name} className="space-y-3 rounded-lg border border-border p-4">
                <legend className="px-1 text-sm font-semibold text-foreground">
                  Stage {index + 1}: {stage.name}
                </legend>
                <p className="text-sm text-muted-foreground">{describeStage(stage)}</p>
                <div className="grid grid-cols-2 gap-3">
                  <NumberField id={`stage-${index}-sessions`} label="Good sessions per step" value={form.sessions} onChange={(v) => setField(index, 'sessions', v)} />
                  {d && (
                    <>
                      <NumberField id={`stage-${index}-min`} label="From" value={form.min} onChange={(v) => setField(index, 'min', v)} />
                      <NumberField id={`stage-${index}-max`} label="Up to" value={form.max} onChange={(v) => setField(index, 'max', v)} />
                      <NumberField
                        id={`stage-${index}-step`}
                        label={d.volumeStepPercent ? 'Step (%)' : 'Step'}
                        value={form.step}
                        onChange={(v) => setField(index, 'step', v)}
                      />
                      {d.intensityUnit !== IntensityUnit.None && (
                        <NumberField
                          id={`stage-${index}-reset`}
                          label="Back to (after a level)"
                          value={form.reset}
                          onChange={(v) => setField(index, 'reset', v)}
                        />
                      )}
                    </>
                  )}
                  {d && d.intensityUnit !== IntensityUnit.None && (
                    <>
                      <NumberField
                        id={`stage-${index}-istart`}
                        label={`${intensityLabel[d.intensityUnit]} start`}
                        value={form.intensityStart}
                        onChange={(v) => setField(index, 'intensityStart', v)}
                      />
                      <NumberField id={`stage-${index}-istep`} label={`${intensityLabel[d.intensityUnit]} step`} value={form.intensityStep} onChange={(v) => setField(index, 'intensityStep', v)} />
                      <NumberField id={`stage-${index}-imax`} label={`${intensityLabel[d.intensityUnit]} top`} value={form.intensityMax} onChange={(v) => setField(index, 'intensityMax', v)} />
                    </>
                  )}
                </div>
                <div className="grid grid-cols-2 gap-3" data-testid={`stage-${index}-segments`}>
                  <NumberField id={`stage-${index}-warm-min`} label="Warm-up min (0 = off)" value={form.warmMinutes} onChange={(v) => setField(index, 'warmMinutes', v)} />
                  <NumberField id={`stage-${index}-warm-level`} label="Warm-up level" value={form.warmLevel} onChange={(v) => setField(index, 'warmLevel', v)} />
                  <NumberField id={`stage-${index}-cool-min`} label="Cool-down min (0 = off)" value={form.coolMinutes} onChange={(v) => setField(index, 'coolMinutes', v)} />
                  <NumberField id={`stage-${index}-cool-level`} label="Cool-down level" value={form.coolLevel} onChange={(v) => setField(index, 'coolLevel', v)} />
                </div>
              </fieldset>
            );
          })}

          <div className="grid grid-cols-2 gap-2">
            <Button type="button" variant="outline" onClick={onClose}>
              Close
            </Button>
            <Button type="submit" disabled={saving}>
              {saving ? 'Saving…' : 'Save'}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
