import type { Meta, StoryObj } from '@storybook/react-vite';
import { useState } from 'react';
import { ExerciseProgressView } from './WorkoutHistoryComponents';
import { TrainingCalendar } from './TrainingCalendar';
import { SessionDetail } from './SessionDetail';
import { OneRepMaxPanel } from './OneRepMaxPanel';
import { HistorySummary } from './HistorySummary';
import { buildCalendarGrid } from './calendarData';
import type { ExerciseHistoryDto, WorkoutActivityDto, WorkoutHistoryDto } from './historyTypes';
import { workoutHistory } from '@/mocks/fixtures/program';

const history = workoutHistory as unknown as WorkoutHistoryDto;
const grid = buildCalendarGrid(history);
const lastSession = history.completedActivities[history.completedActivities.length - 2];

/** History building blocks: the block-coloured calendar, a session's sets, one-rep-max and per-lift progress. */
const meta = {
  title: 'Features/History',
  component: TrainingCalendar,
  decorators: [
    (Story) => (
      <div className="bg-background p-6">
        <Story />
      </div>
    ),
  ],
  args: { grid, exercises: history.exerciseHistories },
} satisfies Meta<typeof TrainingCalendar>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Training days coloured by block; hover one for a summary, click it to see the session. */
function CalendarWithDetail() {
  const [selected, setSelected] = useState<{ activity: WorkoutActivityDto; date: Date }>({
    activity: lastSession,
    date: new Date(lastSession.completedAt),
  });
  return (
    <div className="mx-auto grid max-w-6xl gap-6 lg:grid-cols-3">
      <div className="lg:col-span-2">
        <TrainingCalendar
          grid={grid}
          exercises={history.exerciseHistories}
          selectedDate={selected.date}
          onSelect={(activity, date) => setSelected({ activity, date })}
        />
      </div>
      <SessionDetail activity={selected.activity} date={selected.date} exercises={history.exerciseHistories} />
    </div>
  );
}

export const Calendar: Story = {
  render: () => <CalendarWithDetail />,
};

export const Summary: Story = {
  render: () => (
    <div className="mx-auto max-w-6xl">
      <HistorySummary history={history} />
    </div>
  ),
};

export const Session: Story = {
  render: () => (
    <div className="mx-auto max-w-md">
      <SessionDetail
        activity={lastSession}
        date={new Date(lastSession.completedAt)}
        exercises={history.exerciseHistories}
      />
    </div>
  ),
};

/** Estimated one-rep max per main lift, from each week's AMRAP set. */
export const OneRepMax: Story = {
  render: () => (
    <div className="mx-auto max-w-6xl">
      <OneRepMaxPanel exercises={history.exerciseHistories} />
    </div>
  ),
};

function ExerciseProgressDemo() {
  const [selected, setSelected] = useState<ExerciseHistoryDto | null>(history.exerciseHistories[0]);
  return (
    <div className="mx-auto max-w-6xl">
      <ExerciseProgressView
        exercises={history.exerciseHistories}
        selectedExercise={selected}
        onSelectExercise={setSelected}
      />
    </div>
  );
}

export const ExerciseProgress: Story = {
  render: () => <ExerciseProgressDemo />,
};
