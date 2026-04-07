import type { Meta, StoryObj } from "@storybook/react";
import { UnilateralToggle } from "./UnilateralToggle";
import { fn } from "storybook/test";

const meta: Meta<typeof UnilateralToggle> = {
  title: "Components/UnilateralToggle",
  component: UnilateralToggle,
  parameters: {
    layout: "centered",
  },
  args: {
    isUnilateral: false,
    onChange: fn(),
    disabled: false,
  },
  decorators: [
    (Story) => (
      <div className="w-80">
        <Story />
      </div>
    ),
  ],
};

export default meta;
type Story = StoryObj<typeof UnilateralToggle>;

export const Default: Story = {
  args: {
    isUnilateral: false,
  },
};

export const Enabled: Story = {
  args: {
    isUnilateral: true,
  },
};

export const Disabled: Story = {
  args: {
    isUnilateral: false,
    disabled: true,
  },
};

export const DisabledAndEnabled: Story = {
  args: {
    isUnilateral: true,
    disabled: true,
  },
};
