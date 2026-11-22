import type { Meta, StoryObj } from '@storybook/react';
import { useState } from 'react';
import { withAppProviders, phoneParameters } from '@/mocks/storyProviders';
import { CompletionSummary } from '@/features/workout/CompletionSummary';
import type { CompleteDayResult, WorkoutDto } from '@/features/workout/workoutSessionTypes';
import { GymChecklist } from './GymChecklist';
import { GymHevyStatus } from './GymSections';
import { freshGymSession, midGymSession } from './gymFixtures';
import { allDrillsDone, editSet, tickDrill, toggleSet, updateFinisher, type GymSessionState } from './gymSession';

/** The checklist with working taps (state kept in the story), so each state can be poked at. */
function Checklist({
  initial,
  online = true,
  rest = { remaining: 0, total: 0 },
  initialPrompt = null,
  initialConfirmEarly = false,
}: {
  initial: GymSessionState;
  online?: boolean;
  rest?: { remaining: number; total: number };
  initialPrompt?: { lift: number; set: number } | null;
  initialConfirmEarly?: boolean;
}) {
  const [state, setState] = useState(initial);
  return (
    <div className="min-h-dvh bg-background">
      <GymChecklist
        state={state}
        rest={{ ...rest, running: rest.remaining > 0, onAdjust: () => {}, onSkip: () => {} }}
        online={online}
        finishing={false}
        onTickDrill={(i) => setState((s) => tickDrill(s, i))}
        onAllDrillsDone={() => setState(allDrillsDone)}
        onToggleSet={(l, s, reps) => setState((st) => toggleSet(st, l, s, reps))}
        onEditSet={(l, s, c) => setState((st) => editSet(st, l, s, c))}
        onFinisherChange={(c) => setState((s) => updateFinisher(s, c))}
        onFinish={() => {}}
        initialPrompt={initialPrompt}
        initialConfirmEarly={initialConfirmEarly}
      />
    </div>
  );
}

function allLiftsDone(): GymSessionState {
  let state = allDrillsDone(freshGymSession());
  state.lifts.forEach((lift, li) => lift.sets.forEach((_, si) => (state = toggleSet(state, li, si, lift.sets[si].isAmrap ? 9 : undefined))));
  return state;
}

const meta = {
  title: 'Features/Gym/GymModePage',
  component: Checklist,
  parameters: phoneParameters,
  decorators: [withAppProviders('/gym/1')],
  args: { initial: freshGymSession() },
} satisfies Meta<typeof Checklist>;

export default meta;
type Story = StoryObj<typeof meta>;

/** The start: warm-up drills to tick, then the lifts with the plan's sets pre-filled, then the finisher. */
export const Start: Story = {};

/** Warm-up done, two bench sets ticked. */
export const MidSession: Story = { args: { initial: midGymSession() } };

/** Tapping the AMRAP set asks how many reps were done. */
export const AmrapPrompt: Story = { args: { initial: midGymSession(), initialPrompt: { lift: 0, set: 4 } } };

/** The rest timer runs at the top after a confirmed set (1:35 of 3:00 left). */
export const RestTimer: Story = { args: { initial: midGymSession(), rest: { remaining: 95, total: 180 } } };

/** Every lift done; the stair finisher logged with level, floors and effort, so Finish is ready. */
export const Finisher: Story = {
  args: { initial: updateFinisher(allLiftsDone(), { minutes: 10, level: 8, floors: 42, effort: 'Solid' }) },
};

/** No signal at the gym: sets keep saving on the phone and Finish waits for the connection. */
export const Offline: Story = { args: { initial: midGymSession(), online: false } };

/** Finish early asks first and lists the lifts that will be left out. */
export const FinishEarly: Story = { args: { initial: midGymSession(), initialConfirmEarly: true } };

const workout = { id: 'w-1', name: 'Cut to 85', currentWeek: 3, totalWeeks: 21, daysPerWeek: 4, exercises: [] } as unknown as WorkoutDto;
const result = {
  workoutId: 'w-1',
  day: 1,
  weekNumber: 3,
  blockNumber: 1,
  exercisesCompleted: 2,
  progressionChanges: [
    { exerciseId: 'ex-bench', exerciseName: 'Bench Press (Barbell)', change: 'TM increased by 1% (+4 reps)' },
    { exerciseId: 'ex-row', exerciseName: 'Seal Row (Barbell)', change: 'Maintained' },
  ],
  newCurrentWeek: 3,
  newCurrentDay: 2,
  weekProgressed: false,
  programComplete: false,
  isDeloadWeek: false,
  exercisesPendingWeightConfirmation: [],
  nextSessionExercises: [],
} as unknown as CompleteDayResult;

/** After Finish with Hevy unreachable: progression changes as usual, plus "Not in Hevy yet" and Retry. */
export const FinishedNotInHevy: StoryObj = {
  render: () => (
    <CompletionSummary
      result={result}
      workout={workout}
      dayNumber={1}
      dayName="Monday"
      exerciseEntries={[]}
      onContinue={() => {}}
      hevyNotice={<GymHevyStatus status="Pending" onRetry={() => {}} />}
    />
  ),
};

/** Finished with no signal: saved on the phone, to finish and post when back online. */
export const FinishedOffline: StoryObj = {
  render: () => (
    <div className="min-h-dvh bg-background">
      <main className="mx-auto max-w-2xl px-4 py-8">
        <h1 className="mb-4 text-2xl font-semibold text-foreground">Session saved</h1>
        <GymHevyStatus status={null} queued />
      </main>
    </div>
  ),
};
