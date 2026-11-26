/**
 * Gym mode's one scrolling checklist (brief § 3a): warm-up → each lift → cardio exercises → finisher → Finish. Purely
 * presentational: the page owns the session state and the rest timer and passes the steps in. Big
 * targets for one hand; the rest timer sticks to the top while it runs.
 */

import { useState } from 'react';
import { CheckCircle2, WifiOff } from 'lucide-react';
import { Button } from '@/components/ui/button';
import type { GymFinisher, GymSessionState } from './gymSession';
import { liftBlocks, progressOf } from './gymSession';
import { GymBlockCard } from './GymLiftCard';
import { AmrapPrompt, FinishEarlyDialog } from './GymDialogs';
import { FinisherCard, FixedFinisherCard, RestTimerBar, WarmUpSection } from './GymSections';
import { GymCardioCard } from './GymCardioCard';
import type { CardioSettingsValues } from './CardioSettingsDialog';
import { isCardioDone, type GymCardioChange } from './gymCardio';
import type { Effort } from '@/types/gym';

export interface GymChecklistProps {
  state: GymSessionState;
  rest: { remaining: number; total: number; running: boolean; onAdjust: (seconds: number) => void; onSkip: () => void };
  online: boolean;
  finishing: boolean;
  onTickDrill: (index: number) => void;
  onAllDrillsDone: () => void;
  /** Confirms (or undoes) a set; `reps` is given for the AMRAP set. */
  onToggleSet: (lift: number, set: number, reps?: number) => void;
  onEditSet: (lift: number, set: number, change: { weight?: number; reps?: number }) => void;
  onFinisherChange: (change: Partial<Pick<GymFinisher, 'minutes' | 'workMinutes' | 'level' | 'floors' | 'effort' | 'pain'>>) => void;
  onFinish: () => void;
  /** Ticks the extra set ("+1 left"); with a change, edits its weight or reps. */
  onToggleExtraSet?: (lift: number, change?: { weight?: number; reps?: number }) => void;
  /** Ticks the fixed cool-down. */
  onToggleFixedFinisher?: () => void;
  /** Opens the live finisher wizard (stair finisher only). */
  onStartFinisher?: () => void;
  finisherStartLabel?: string;
  /** Opens "Set your training max" for a lift without one. */
  onSetTrainingMax?: (lift: number) => void;
  /** Unilateral lifts: ticks a set's left side (done first). */
  onToggleLeft?: (lift: number, set: number) => void;
  /** Cardio exercises (by index into `state.cardio`): log fields, Easy / Solid / Brutal, the walk's tick. */
  onCardioChange?: (index: number, change: GymCardioChange) => void;
  onCardioEffort?: (index: number, effort: Effort) => void;
  onTickCardio?: (index: number) => void;
  /** A cardio exercise's blank starting values chosen with no signal. */
  onCardioSettingsChosen?: (index: number, values: CardioSettingsValues) => void;
  /** Opens the live wizard for the stair climber. */
  onStartCardio?: (index: number) => void;
  cardioStartLabel?: string;
  /** Red readiness skipped the stair climber: keep it after all. */
  onKeepHardCardio?: () => void;
  /** Stories and tests: open with the AMRAP prompt or the Finish early dialog showing. */
  initialPrompt?: { lift: number; set: number } | null;
  initialConfirmEarly?: boolean;
}

export function GymChecklist({
  state,
  rest,
  online,
  finishing,
  onTickDrill,
  onAllDrillsDone,
  onToggleSet,
  onEditSet,
  onFinisherChange,
  onFinish,
  onToggleExtraSet,
  onToggleFixedFinisher,
  onStartFinisher,
  finisherStartLabel,
  onSetTrainingMax,
  onToggleLeft,
  onCardioChange,
  onCardioEffort,
  onTickCardio,
  onCardioSettingsChosen,
  onStartCardio,
  cardioStartLabel,
  onKeepHardCardio,
  initialPrompt = null,
  initialConfirmEarly = false,
}: GymChecklistProps) {
  const [prompt, setPrompt] = useState(initialPrompt);
  const [confirmEarly, setConfirmEarly] = useState(initialConfirmEarly);
  const progress = progressOf(state);
  const blocks = liftBlocks(state);
  const activeBlock = blocks.findIndex((b) => b.lifts.some((l) => state.lifts[l].sets.some((s) => !s.done)));
  const promptLift = prompt ? state.lifts[prompt.lift] : null;
  const promptSet = prompt && promptLift ? promptLift.sets[prompt.set] : null;
  const skipped = [
    ...state.lifts.filter((l) => l.sets.every((s) => !s.done)).map((l) => l.name),
    ...state.cardio.filter((c) => !c.skipped && !isCardioDone(c)).map((c) => c.name),
  ];

  return (
    <div>
      {rest.running && <RestTimerBar remaining={rest.remaining} total={rest.total} onAdjust={rest.onAdjust} onSkip={rest.onSkip} />}

      <main className="mx-auto max-w-2xl space-y-4 px-4 py-6">
        <header>
          <p className="text-sm text-muted-foreground">Gym mode · Week {state.week}</p>
          <h1 className="text-hero">{state.title}</h1>
          <p className="mt-1 text-sm text-muted-foreground" aria-live="polite" data-testid="gym-progress">
            {progress.setsDone} of {progress.setsTotal} sets done
          </p>
          <div className="mt-2 h-1 overflow-hidden rounded-full bg-foreground/10" aria-hidden="true">
            <div
              className="h-full rounded-full bg-primary transition-[width] duration-300 ease-out"
              style={{ width: `${progress.setsTotal > 0 ? Math.round((progress.setsDone / progress.setsTotal) * 100) : 0}%` }}
            />
          </div>
        </header>

        {!online && (
          <p className="flex items-center gap-2 rounded-md bg-foreground/[0.06] p-3 text-sm text-foreground" data-testid="gym-offline">
            <WifiOff className="h-4 w-4 shrink-0" aria-hidden="true" />
            Offline. Every set is saved on this phone; Finish waits until you are back online.
          </p>
        )}

        {state.drills.length > 0 && <WarmUpSection drills={state.drills} onTick={onTickDrill} onAllDone={onAllDrillsDone} />}

        {blocks.map((block, blockIndex) => (
          <GymBlockCard
            key={state.lifts[block.lifts[0]].exerciseId}
            block={block}
            active={blockIndex === activeBlock}
            lifts={state.lifts}
            onToggleSet={(l, s) => onToggleSet(l, s)}
            onAmrap={(l, s) => setPrompt({ lift: l, set: s })}
            onEditSet={onEditSet}
            onToggleExtraSet={onToggleExtraSet}
            onSetTrainingMax={onSetTrainingMax}
            onToggleLeft={onToggleLeft}
          />
        ))}

        {state.cardio.map((item, i) => (
          <GymCardioCard
            key={item.exerciseId}
            item={item}
            workoutId={state.workoutId}
            onChange={(change) => onCardioChange?.(i, change)}
            onEffort={(effort) => onCardioEffort?.(i, effort)}
            onTick={() => onTickCardio?.(i)}
            onStart={onStartCardio ? () => onStartCardio(i) : undefined}
            startLabel={cardioStartLabel}
            onKeep={onKeepHardCardio}
            onSettingsChosen={(values) => onCardioSettingsChosen?.(i, values)}
          />
        ))}

        {state.finisher?.fixed ? (
          <FixedFinisherCard finisher={state.finisher} onToggle={() => onToggleFixedFinisher?.()} />
        ) : (
          state.finisher && <FinisherCard finisher={state.finisher} onChange={onFinisherChange} onStart={onStartFinisher} startLabel={finisherStartLabel} />
        )}

        <div className="space-y-2 pb-8 pt-2">
          {progress.complete ? (
            <Button type="button" size="lg" className="w-full" onClick={onFinish} disabled={finishing || progress.needsTrainingMax.length > 0} data-testid="gym-finish">
              <CheckCircle2 aria-hidden="true" />
              {finishing ? 'Finishing…' : 'Finish'}
            </Button>
          ) : (
            <Button
              type="button"
              size="lg"
              variant="outline"
              className="w-full"
              onClick={() => setConfirmEarly(true)}
              disabled={!progress.canFinish || finishing}
              data-testid="gym-finish-early"
            >
              {finishing ? 'Finishing…' : 'Finish early'}
            </Button>
          )}
          {progress.needsTrainingMax.length > 0 ? (
            <p className="text-center text-sm text-foreground" role="alert" data-testid="gym-needs-tm">
              Set your training max for {progress.needsTrainingMax.join(' and ')} before finishing, or untick its sets to leave it out.
            </p>
          ) : (
            !progress.canFinish && <p className="text-center text-sm text-muted-foreground">Tick at least one set to finish.</p>
          )}
        </div>
      </main>

      {prompt && promptLift && promptSet && (
        <AmrapPrompt
          open
          exerciseName={promptLift.name}
          weightLabel={promptLift.trainingMaxPending && promptSet.weight === 0 ? 'a weight to be set' : `${promptSet.weight} ${promptLift.unit}`}
          targetReps={promptSet.plannedReps}
          onCancel={() => setPrompt(null)}
          onSubmit={(reps) => {
            onToggleSet(prompt.lift, prompt.set, reps);
            setPrompt(null);
          }}
        />
      )}

      <FinishEarlyDialog
        open={confirmEarly}
        skipped={skipped}
        setsLeft={progress.setsTotal - progress.setsDone}
        onCancel={() => setConfirmEarly(false)}
        onConfirm={() => {
          setConfirmEarly(false);
          onFinish();
        }}
      />
    </div>
  );
}
