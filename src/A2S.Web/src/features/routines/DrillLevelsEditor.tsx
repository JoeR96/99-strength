/**
 * The levels of one drill in the routine editor: Easier / Standard / Harder, or a Level 1-3 ladder. Each level
 * has a name, description, sets, amount, an optional range (e.g. hold 15 to 30 s, +5 s a step) and cues.
 * Levels reorder with up/down buttons (WCAG 2.5.7); the starting level picks where the drill begins.
 */

import { ArrowDown, ArrowUp, Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { doseUnitLabel } from '@/types/mobility';
import { emptyLevel, moveItem, type DrillDraft, type LevelDraft, type LevelField } from './routineForm';

interface Props {
  drill: DrillDraft;
  drillNumber: number;
  errors: Record<string, Partial<Record<LevelField, string>>>;
  onChange: (patch: Partial<DrillDraft>) => void;
  selectClass: string;
}

export function DrillLevelsEditor({ drill, drillNumber, errors, onChange, selectClass }: Props) {
  const levels = drill.levels;
  const unit = doseUnitLabel[drill.unit].many;

  const setLevels = (next: LevelDraft[], startingLevel = drill.startingLevel) =>
    onChange({ levels: next, startingLevel: Math.min(Math.max(startingLevel, 0), Math.max(next.length - 1, 0)) });
  const updateLevel = (key: string, patch: Partial<LevelDraft>) => setLevels(levels.map((l) => (l.key === key ? { ...l, ...patch } : l)));

  const move = (index: number, by: -1 | 1) => {
    const startKey = levels[drill.startingLevel]?.key;
    const next = moveItem(levels, index, by);
    setLevels(next, Math.max(0, next.findIndex((l) => l.key === startKey)));
  };

  const remove = (index: number) => {
    const startKey = levels[drill.startingLevel]?.key;
    const next = levels.filter((_, i) => i !== index);
    const kept = next.findIndex((l) => l.key === startKey);
    setLevels(next, kept >= 0 ? kept : 0);
  };

  return (
    <fieldset className="space-y-3 rounded-lg border border-border p-3">
      <legend className="px-1 text-sm font-medium text-foreground">Levels</legend>
      {levels.length === 0 && (
        <p className="text-sm text-muted-foreground">Add an easier and a harder version, or a ladder, and the app moves you on after two clean sessions.</p>
      )}

      {levels.length > 0 && (
        <div>
          <label htmlFor={`${drill.key}-start`} className="mb-1 block text-sm font-medium text-foreground">
            Starting level
          </label>
          <select id={`${drill.key}-start`} className={selectClass} value={drill.startingLevel} onChange={(e) => onChange({ startingLevel: Number(e.target.value) })}>
            {levels.map((l, i) => (
              <option key={l.key} value={i}>
                {l.name || `Level ${i + 1}`}
              </option>
            ))}
          </select>
        </div>
      )}

      <ol className="space-y-3">
        {levels.map((level, index) => {
          const e = errors[level.key] ?? {};
          const fid = (f: string) => `${level.key}-${f}`;
          const label = `drill ${drillNumber} level ${index + 1}`;
          return (
            <li key={level.key} className="space-y-2 rounded-lg border border-border bg-muted/20 p-3" aria-label={`Level ${index + 1}${level.name ? `: ${level.name}` : ''}`}>
              <div className="flex items-center justify-between gap-2">
                <span className="text-sm font-medium text-muted-foreground">Level {index + 1}</span>
                <div className="flex gap-1">
                  <Button type="button" variant="ghost" size="icon" className="h-11 w-11" disabled={index === 0} aria-label={`Move ${label} up`} onClick={() => move(index, -1)}>
                    <ArrowUp aria-hidden="true" />
                  </Button>
                  <Button type="button" variant="ghost" size="icon" className="h-11 w-11" disabled={index === levels.length - 1} aria-label={`Move ${label} down`} onClick={() => move(index, 1)}>
                    <ArrowDown aria-hidden="true" />
                  </Button>
                  <Button type="button" variant="ghost" size="icon" className="h-11 w-11" aria-label={`Remove ${label}`} onClick={() => remove(index)}>
                    <Trash2 aria-hidden="true" />
                  </Button>
                </div>
              </div>
              <div className="grid grid-cols-2 gap-2">
                <div>
                  <label htmlFor={fid('name')} className="mb-1 block text-sm font-medium text-foreground">
                    Name
                  </label>
                  <Input id={fid('name')} value={level.name} aria-invalid={!!e.name} onChange={(ev) => updateLevel(level.key, { name: ev.target.value })} />
                  {e.name && <p className="mt-1 text-sm text-destructive">{e.name}</p>}
                </div>
                <div>
                  <label htmlFor={fid('desc')} className="mb-1 block text-sm font-medium text-foreground">
                    What it is
                  </label>
                  <Input id={fid('desc')} value={level.description} onChange={(ev) => updateLevel(level.key, { description: ev.target.value })} placeholder="Hands on a bench" />
                </div>
              </div>
              <div className="grid grid-cols-2 gap-2">
                <div>
                  <label htmlFor={fid('sets')} className="mb-1 block text-sm font-medium text-foreground">
                    Sets
                  </label>
                  <Input id={fid('sets')} inputMode="numeric" value={level.sets} aria-invalid={!!e.sets} onChange={(ev) => updateLevel(level.key, { sets: ev.target.value })} />
                  {e.sets && <p className="mt-1 text-sm text-destructive">{e.sets}</p>}
                </div>
                <div>
                  <label htmlFor={fid('amount')} className="mb-1 block text-sm font-medium text-foreground">
                    Amount ({unit})
                  </label>
                  <Input id={fid('amount')} inputMode="numeric" value={level.amount} aria-invalid={!!e.amount} onChange={(ev) => updateLevel(level.key, { amount: ev.target.value })} />
                  {e.amount && <p className="mt-1 text-sm text-destructive">{e.amount}</p>}
                </div>
              </div>
              <div>
                <p className="mb-1 text-sm font-medium text-foreground">
                  Range <span className="font-normal text-muted-foreground">(optional, in {unit})</span>
                </p>
                <div className="grid grid-cols-3 gap-2">
                  <Input aria-label={`${label} range from`} inputMode="numeric" placeholder="From" value={level.rangeMin} aria-invalid={!!e.range} onChange={(ev) => updateLevel(level.key, { rangeMin: ev.target.value })} />
                  <Input aria-label={`${label} range to`} inputMode="numeric" placeholder="To" value={level.rangeMax} aria-invalid={!!e.range} onChange={(ev) => updateLevel(level.key, { rangeMax: ev.target.value })} />
                  <Input aria-label={`${label} range step`} inputMode="numeric" placeholder="Step" value={level.rangeStep} aria-invalid={!!e.range} onChange={(ev) => updateLevel(level.key, { rangeStep: ev.target.value })} />
                </div>
                {e.range && <p className="mt-1 text-sm text-destructive">{e.range}</p>}
              </div>
              <div>
                <label htmlFor={fid('cues')} className="mb-1 block text-sm font-medium text-foreground">
                  Cues <span className="font-normal text-muted-foreground">(optional)</span>
                </label>
                <Input id={fid('cues')} value={level.cues} onChange={(ev) => updateLevel(level.key, { cues: ev.target.value })} placeholder="Body in one line" />
              </div>
            </li>
          );
        })}
      </ol>

      <Button type="button" variant="outline" className="h-11 w-full" onClick={() => setLevels([...levels, emptyLevel(drill)])} aria-label={`Add level to drill ${drillNumber}`}>
        <Plus aria-hidden="true" />
        Add level
      </Button>
    </fieldset>
  );
}
