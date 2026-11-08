import type { Meta, StoryObj } from '@storybook/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fn } from 'storybook/test';
import { StartSuggestedPlanCard } from './StartSuggestedPlanCard';
import { suggestedPlansApi, type StartSuggestedPlanResult } from '@/api/suggestedPlans';

const wednesday11Nov2026 = new Date(2026, 10, 11);

const started: StartSuggestedPlanResult = {
  planId: 'plan-1',
  workoutId: 'workout-1',
  startDate: '2026-11-16',
  alreadyStarted: false,
  warnings: [],
};

const meta: Meta<typeof StartSuggestedPlanCard> = {
  title: 'Today/StartSuggestedPlanCard',
  component: StartSuggestedPlanCard,
  parameters: {
    layout: 'padded',
    viewport: { defaultViewport: 'mobile2' },
  },
  args: {
    today: wednesday11Nov2026,
    onStarted: fn(),
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })}>
        <div className="mx-auto w-full max-w-[390px] px-4">
          <Story />
        </div>
      </QueryClientProvider>
    ),
  ],
};

export default meta;
type Story = StoryObj<typeof StartSuggestedPlanCard>;

/** First run: empty training maxes, start weight prefilled 95 kg, starts Monday 16 Nov. */
export const FirstRun: Story = {
  beforeEach: () => {
    suggestedPlansApi.startCutTo85 = async () => {
      await new Promise((resolve) => setTimeout(resolve, 600));
      return started;
    };
  },
};

/** Fields prefilled from a previous programme. */
export const Prefilled: Story = {
  args: {
    initialValues: { benchTm: 100, squatTm: 140, ohpTm: 60, deadliftTm: 180, startWeightKg: 96.5 },
  },
  beforeEach: () => {
    suggestedPlansApi.startCutTo85 = async () => started;
  },
};

/** The server refuses (e.g. a template missing from the library): the message shows under the button. */
export const ServerError: Story = {
  args: {
    initialValues: { benchTm: 100, squatTm: 140, ohpTm: 60, deadliftTm: 180 },
  },
  beforeEach: () => {
    suggestedPlansApi.startCutTo85 = async () => {
      throw { response: { data: { title: 'Business Rule Violation', detail: "Exercise template 'Seal Row (Barbell)' was not found in the exercise library." } } };
    };
  },
};
