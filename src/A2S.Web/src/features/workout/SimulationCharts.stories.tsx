import type { Meta, StoryObj } from '@storybook/react-vite';
import { TrainingMaxProjectionChart, AccessoryWeightProjectionChart } from './SimulationCharts';
import { simulationResult } from '@/mocks/fixtures/simulation';
import type { ExerciseSimulationSeries } from './simulationTypes';

const projection = simulationResult(40);
const linear = projection.exerciseTimeSeries.filter(
  (s) => s.progressionType === 'Linear'
) as ExerciseSimulationSeries[];
const accessories = projection.exerciseTimeSeries.filter(
  (s) => s.progressionType === 'RepsPerSet'
) as ExerciseSimulationSeries[];

/** The simulator's projection charts: 40 sessions from week 11 to the end of the program. */
const meta = {
  title: 'Features/Charts/Simulation Charts',
  component: TrainingMaxProjectionChart,
  decorators: [
    (Story) => (
      <div className="bg-background p-6">
        <div className="mx-auto max-w-5xl">
          <Story />
        </div>
      </div>
    ),
  ],
  args: { series: linear },
} satisfies Meta<typeof TrainingMaxProjectionChart>;

export default meta;
type Story = StoryObj<typeof meta>;

export const TrainingMaxes: Story = {};

export const AccessoryWeights: Story = {
  args: { series: accessories },
  render: (args) => <AccessoryWeightProjectionChart series={args.series} />,
};
