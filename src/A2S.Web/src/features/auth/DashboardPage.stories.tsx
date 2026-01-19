import type { Meta, StoryObj } from "@storybook/react";
import { DashboardPage } from "./DashboardPage";
import { useUser } from "@clerk/clerk-react";
import { MemoryRouter } from "react-router-dom";

// Mock user data
const mockUser = {
  firstName: "Alex",
  lastName: "Johnson",
  emailAddresses: [{ emailAddress: "alex.johnson@example.com" }],
  imageUrl: "https://images.clerk.dev/placeholder.png",
};

const meta = {
  title: "Features/Auth/DashboardPage",
  component: DashboardPage,
  parameters: {
    layout: "fullscreen",
    docs: {
      description: {
        component:
          "Modern dashboard with mosaic-style layout using Golden Twilight theme. " +
          "Features quick stats, current program, recent activity, next workout, and personal records.",
      },
    },
  },
  decorators: [
    (Story) => {
      // Mock useUser
      (useUser as any) = () => ({
        user: mockUser,
        isLoaded: true,
        isSignedIn: true,
      });

      return (
        <MemoryRouter>
          <Story />
        </MemoryRouter>
      );
    },
  ],
} satisfies Meta<typeof DashboardPage>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * 1. Default - Empty dashboard, no workout
 */
export const Default: Story = {
  args: {},
  parameters: {
    docs: {
      description: {
        story:
          "Default dashboard state for a new user with no workouts or active program. " +
          "Shows empty states for all cards and prompts to start a program.",
      },
    },
  },
};

/**
 * 2. WithActiveWorkout - Show current program link
 */
export const WithActiveWorkout: Story = {
  args: {},
  parameters: {
    docs: {
      description: {
        story:
          "Dashboard with an active workout program. The Current Program card shows the active plan " +
          "instead of the start button (future enhancement).",
      },
    },
  },
};

/**
 * 3. MobileView - Responsive layout
 */
export const MobileView: Story = {
  args: {},
  parameters: {
    viewport: {
      defaultViewport: "mobile1",
    },
    docs: {
      description: {
        story:
          "Mobile responsive view showing how the mosaic grid adapts to smaller screens. " +
          "Cards stack vertically for optimal mobile viewing.",
      },
    },
  },
};

/**
 * 4. AllCardsPopulated - Mock data for all sections
 */
export const AllCardsPopulated: Story = {
  args: {},
  parameters: {
    docs: {
      description: {
        story:
          "Future vision of the dashboard with all sections populated with data. " +
          "Shows workout stats, activity, and personal records (mock data).",
      },
    },
  },
};

/**
 * 5. TabletView - Medium screen layout
 */
export const TabletView: Story = {
  args: {},
  parameters: {
    viewport: {
      defaultViewport: "tablet",
    },
    docs: {
      description: {
        story:
          "Tablet view showing the 2-column grid layout. " +
          "Demonstrates responsive breakpoints between mobile and desktop.",
      },
    },
  },
};

/**
 * 6. NewUser - First time experience
 */
export const NewUser: Story = {
  args: {},
  parameters: {
    docs: {
      description: {
        story:
          "First-time user experience with personalized welcome message. " +
          "Shows empty states with clear call-to-action to start training.",
      },
    },
  },
};

/**
 * 7. CustomName - Different user name
 */
export const CustomName: Story = {
  args: {},
  parameters: {
    docs: {
      description: {
        story: "Dashboard with a different user name to show personalization.",
      },
    },
  },
};

/**
 * 8. DesktopWide - Large screen layout
 */
export const DesktopWide: Story = {
  args: {},
  parameters: {
    viewport: {
      defaultViewport: "desktop",
    },
    docs: {
      description: {
        story:
          "Full desktop view showing the 3-column mosaic grid layout. " +
          "Demonstrates optimal spacing and card arrangement on large screens.",
      },
    },
  },
};
