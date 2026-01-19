import type { Meta, StoryObj } from '@storybook/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { SetupWizard } from './SetupWizard';
import { EquipmentType } from '@/types/workout';
import type { ExerciseTemplate } from '@/types/workout';
import { workoutsApi } from '@/api/workouts';

// Mock exercise templates for the library
const mockTemplates: ExerciseTemplate[] = [
  {
    name: 'Squat',
    equipment: EquipmentType.Barbell,
    defaultRepRange: { minimum: 3, target: 5, maximum: 7 },
    defaultSets: 4,
    description: 'Back Squat - primary lower body compound movement',
  },
  {
    name: 'Bench Press',
    equipment: EquipmentType.Barbell,
    defaultRepRange: { minimum: 3, target: 5, maximum: 7 },
    defaultSets: 4,
    description: 'Barbell Bench Press - primary pushing movement',
  },
  {
    name: 'Deadlift',
    equipment: EquipmentType.Barbell,
    defaultRepRange: { minimum: 3, target: 5, maximum: 7 },
    defaultSets: 4,
    description: 'Conventional Deadlift - primary pulling movement',
  },
  {
    name: 'Overhead Press',
    equipment: EquipmentType.Barbell,
    defaultRepRange: { minimum: 3, target: 5, maximum: 7 },
    defaultSets: 4,
    description: 'Standing Barbell Overhead Press - vertical pressing movement',
  },
  {
    name: 'Front Squat',
    equipment: EquipmentType.Barbell,
    defaultRepRange: { minimum: 3, target: 5, maximum: 7 },
    defaultSets: 4,
    description: 'Front-loaded squat variation',
  },
  {
    name: 'Romanian Deadlift',
    equipment: EquipmentType.Barbell,
    defaultRepRange: { minimum: 5, target: 8, maximum: 11 },
    defaultSets: 3,
    description: 'Hip-hinge movement targeting hamstrings',
  },
  {
    name: 'Lat Pulldown',
    equipment: EquipmentType.Cable,
    defaultRepRange: { minimum: 5, target: 8, maximum: 11 },
    defaultSets: 3,
    description: 'Cable lat pulldown for back width',
  },
  {
    name: 'Face Pull',
    equipment: EquipmentType.Cable,
    defaultRepRange: { minimum: 8, target: 12, maximum: 16 },
    defaultSets: 3,
    description: 'Cable face pull for rear deltoids',
  },
];

/**
 * SetupWizard is a multi-step wizard that guides users through creating a new A2S workout program.
 *
 * ## Wizard Steps
 * 1. **Welcome** - Configure program name, variant (4/5/6-day), and total weeks
 * 2. **Training Maxes** - Set training maxes for the four main lifts
 * 3. **Exercises** - Select auxiliary and accessory exercises
 * 4. **Confirm** - Review all selections before creating the workout
 *
 * ## Features
 * - Progress indicator showing current step
 * - Back/Next navigation with validation
 * - Step validation before proceeding
 * - Final confirmation screen with summary
 * - Loading state during workout creation
 */
const meta = {
  title: 'Workout/SetupWizard',
  component: SetupWizard,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component: 'Multi-step wizard for creating a new A2S workout program with program configuration, training maxes, and exercise selection.',
      },
    },
  },
  tags: ['autodocs'],
  decorators: [
    (Story) => {
      const queryClient = new QueryClient({
        defaultOptions: {
          queries: {
            retry: false,
          },
        },
      });

      // Mock the exercise library API
      workoutsApi.getExerciseLibrary = async () => ({
        templates: mockTemplates,
      });

      return (
        <QueryClientProvider client={queryClient}>
          <MemoryRouter>
            <div className="min-h-screen bg-background p-6">
              <Story />
            </div>
          </MemoryRouter>
        </QueryClientProvider>
      );
    },
  ],
} satisfies Meta<typeof SetupWizard>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default wizard starting at the welcome step.
 * This is the first screen users see when creating a new workout program.
 */
export const WelcomeStep: Story = {
  parameters: {
    docs: {
      description: {
        story: 'The welcome screen where users configure their program name, variant (4/5/6 day), and total weeks.',
      },
    },
  },
};

/**
 * Interactive wizard - users can navigate through all steps.
 * Try clicking Next to progress through the wizard steps.
 */
export const Interactive: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Fully interactive wizard. Click Next/Back to navigate between steps and see the complete workout creation flow.',
      },
    },
  },
};

/**
 * Wizard configured for a 5-day program variant.
 */
export const FiveDayVariant: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Shows the wizard configured for a 5-day training split.',
      },
    },
  },
};

/**
 * Wizard configured for a 6-day program variant.
 */
export const SixDayVariant: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Shows the wizard configured for a 6-day training split.',
      },
    },
  },
};

/**
 * Shortened program duration (12 weeks instead of standard 21).
 */
export const ShortProgram: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Example of a shorter 12-week program instead of the standard 21 weeks.',
      },
    },
  },
};

/**
 * Custom program name entered.
 */
export const CustomProgramName: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Shows the wizard with a custom program name like "Summer Strength Block 2026".',
      },
    },
  },
};

/**
 * Progress indicator showing all four steps.
 */
export const ProgressIndicator: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Highlights the progress indicator that shows users where they are in the 4-step wizard process.',
      },
    },
  },
};

/**
 * Wizard with validation - empty program name should disable Next.
 */
export const ValidationExample: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Demonstrates validation - the Next button is disabled when the program name is empty.',
      },
    },
  },
};

/**
 * Mobile responsive view of the wizard.
 */
export const MobileView: Story = {
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    docs: {
      description: {
        story: 'Shows how the wizard adapts to mobile screen sizes.',
      },
    },
  },
};

/**
 * Tablet responsive view.
 */
export const TabletView: Story = {
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
    docs: {
      description: {
        story: 'Shows how the wizard appears on tablet devices.',
      },
    },
  },
};

/**
 * Wizard demonstrating the complete flow with all features.
 * This story is useful for testing the entire user journey.
 */
export const CompleteFlow: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Complete wizard flow from start to finish. Navigate through all 4 steps to see the entire workout creation process.',
      },
    },
  },
};

/**
 * Beginner user creating their first program with default values.
 */
export const BeginnerSetup: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Example of a beginner user setting up their first program with conservative training maxes.',
      },
    },
  },
};

/**
 * Advanced user creating a custom high-volume program.
 */
export const AdvancedSetup: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Example of an advanced lifter creating a 6-day program with many accessory exercises.',
      },
    },
  },
};

/**
 * Loading state when creating the workout.
 * Shows what happens when the user clicks "Create Program" and the API request is processing.
 */
export const CreatingWorkout: Story = {
  decorators: [
    (Story) => {
      const queryClient = new QueryClient({
        defaultOptions: {
          queries: {
            retry: false,
          },
        },
      });

      // Mock the exercise library API
      workoutsApi.getExerciseLibrary = async () => ({
        templates: mockTemplates,
      });

      // Mock a pending mutation
      queryClient.setMutationDefaults(['createWorkout'], {
        mutationFn: () => new Promise(() => {}), // Never resolves to show loading state
      });

      return (
        <QueryClientProvider client={queryClient}>
          <MemoryRouter>
            <div className="min-h-screen bg-background p-6">
              <Story />
            </div>
          </MemoryRouter>
        </QueryClientProvider>
      );
    },
  ],
  parameters: {
    docs: {
      description: {
        story: 'Shows the loading state when the workout is being created.',
      },
    },
  },
};

/**
 * Educational info boxes visible throughout the wizard.
 */
export const WithEducationalContent: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Highlights the educational content boxes that help users understand concepts like Training Max.',
      },
    },
  },
};
