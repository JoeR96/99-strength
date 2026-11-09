import type { Meta, StoryObj } from '@storybook/react';
import { ProgressPage } from './ProgressPage';
import { withAppProviders, withApiMocks, phoneParameters } from '@/mocks/storyProviders';
import { installPlansHandlers, plansHandlers } from '@/mocks/handlers/plans';
import { installBodyWeightHandlers, bodyWeightHandlers } from '@/mocks/handlers/bodyWeight';
import { installBudgetHandlers, budgetHandlers } from '@/mocks/handlers/budget';
import { installWorkoutsHandlers, workoutsHandlers } from '@/mocks/handlers/workouts';

const meta = {
  title: 'Features/Progress/ProgressPage',
  component: ProgressPage,
  parameters: phoneParameters,
  decorators: [withAppProviders('/progress')],
} satisfies Meta<typeof ProgressPage>;

export default meta;
type Story = StoryObj<typeof meta>;

function scenario(bodyWeight: () => void, plans: () => void = () => installPlansHandlers(plansHandlers.default()), workouts: () => void = () => installWorkoutsHandlers(workoutsHandlers.default()), budget: () => void = () => installBudgetHandlers(budgetHandlers.default())) {
  return withApiMocks(() => {
    plans();
    bodyWeight();
    workouts();
    budget();
  });
}

export const OnPace: Story = {
  decorators: [scenario(() => installBodyWeightHandlers(bodyWeightHandlers.onPace()))],
};

export const Behind: Story = {
  decorators: [scenario(() => installBodyWeightHandlers(bodyWeightHandlers.behind()))],
};

export const Ahead: Story = {
  decorators: [scenario(() => installBodyWeightHandlers(bodyWeightHandlers.ahead()))],
};

/** No plan: the trend still draws, there is no target line and no checkpoints. */
export const NoTarget: Story = {
  decorators: [
    scenario(
      () => installBodyWeightHandlers(bodyWeightHandlers.noTarget()),
      () => installPlansHandlers(plansHandlers.noPlan()),
      () => installWorkoutsHandlers(workoutsHandlers.none())
    ),
  ],
};

/** Fewer than seven weigh-ins: no 7-day average line yet. */
export const FewPoints: Story = {
  decorators: [scenario(() => installBodyWeightHandlers(bodyWeightHandlers.fewPoints()))],
};

export const Empty: Story = {
  decorators: [scenario(() => installBodyWeightHandlers(bodyWeightHandlers.empty()), undefined, undefined, () => installBudgetHandlers(budgetHandlers.noWarnings()))],
};

export const Loading: Story = {
  decorators: [
    scenario(
      () => installBodyWeightHandlers(bodyWeightHandlers.loading()),
      () => installPlansHandlers(plansHandlers.loading()),
      () => installWorkoutsHandlers(workoutsHandlers.loading()),
      () => installBudgetHandlers(budgetHandlers.loading())
    ),
  ],
};

export const Error: Story = {
  decorators: [
    scenario(
      () => installBodyWeightHandlers(bodyWeightHandlers.error()),
      () => installPlansHandlers(plansHandlers.error()),
      () => installWorkoutsHandlers(workoutsHandlers.error()),
      () => installBudgetHandlers(budgetHandlers.error())
    ),
  ],
};
