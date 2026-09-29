import type { Meta, StoryObj } from '@storybook/react-vite';
import { useState } from 'react';
import {
  GitHubStyleCalendar,
  WorkoutActivityDetail,
  ExerciseProgressView,
  type ExerciseHistoryDto,
  type WorkoutActivityDto,
  type WorkoutHistoryDto,
} from './WorkoutHistoryComponents';
import { buildCalendarMonths } from './calendarData';
import { workoutHistory } from '@/mocks/fixtures/program';

const history = workoutHistory as unknown as WorkoutHistoryDto;
const months = buildCalendarMonths(history);
const lastSession = history.completedActivities[history.completedActivities.length - 2];

/** History building blocks: the block-coloured calendar, a session's detail, per-lift progress. */
const meta = {
  title: 'Features/History',
  component: GitHubStyleCalendar,
  decorators: [
    (Story) => (
      <div className="bg-background p-6">
        <Story />
      </div>
    ),
  ],
  args: { months, daysPerWeek: history.daysPerWeek },
} satisfies Meta<typeof GitHubStyleCalendar>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Training days coloured by block; click one to see the session. */
function CalendarWithDetail(args: { months: typeof months; daysPerWeek: number }) {
  const [selected, setSelected] = useState<{ activity: WorkoutActivityDto; date: Date } | null>(
    null
  );
  return (
    <div className="mx-auto grid max-w-6xl gap-6 lg:grid-cols-3">
      <div className="lg:col-span-2">
        <GitHubStyleCalendar
          {...args}
          selectedDate={selected?.date}
          onActivityClick={(activity, date) => setSelected({ activity, date })}
        />
      </div>
      <WorkoutActivityDetail
        activity={selected?.activity}
        date={selected?.date}
        exerciseHistories={history.exerciseHistories}
        onClose={() => setSelected(null)}
      />
    </div>
  );
}

export const Calendar: Story = {
  render: (args) => <CalendarWithDetail {...args} />,
};

export const SessionDetail: Story = {
  render: () => (
    <div className="mx-auto max-w-md">
      <WorkoutActivityDetail
        activity={lastSession}
        date={new Date(lastSession.completedAt)}
        exerciseHistories={history.exerciseHistories}
        onClose={() => {}}
      />
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
