import type { Meta, StoryObj } from "@storybook/react";
import { UndoConfirmationModal } from "./UndoConfirmationModal";
import { fn } from "storybook/test";

const meta: Meta<typeof UndoConfirmationModal> = {
  title: "Components/UndoConfirmationModal",
  component: UndoConfirmationModal,
  parameters: {
    layout: "centered",
  },
  args: {
    isOpen: true,
    onClose: fn(),
    onConfirm: fn(async () => {}),
    dayNumber: 1,
    weekNumber: 3,
    wouldRollbackWeek: false,
  },
};

export default meta;
type Story = StoryObj<typeof UndoConfirmationModal>;

export const Default: Story = {
  args: {
    dayNumber: 1,
    weekNumber: 3,
    wouldRollbackWeek: false,
  },
};

export const Wednesday: Story = {
  args: {
    dayNumber: 3,
    weekNumber: 5,
    wouldRollbackWeek: false,
  },
};

export const WithWeekRollback: Story = {
  args: {
    dayNumber: 4,
    weekNumber: 7,
    wouldRollbackWeek: true,
  },
};

export const LastDayOfWeek: Story = {
  args: {
    dayNumber: 6,
    weekNumber: 10,
    wouldRollbackWeek: true,
  },
};

export const Closed: Story = {
  args: {
    isOpen: false,
  },
};
