import type { Meta, StoryObj } from '@storybook/react';
import { LoginForm } from './LoginForm';

/**
 * LoginForm component displays a custom sign-in form using ShadCN UI components.
 * It's designed to work with Clerk authentication but can be used with any auth provider.
 */
const meta = {
  title: 'Auth/LoginForm',
  component: LoginForm,
  parameters: {
    layout: 'centered',
  },
  tags: ['autodocs'],
  argTypes: {
    isLoading: {
      control: 'boolean',
      description: 'Shows loading state on the submit button',
    },
    error: {
      control: 'text',
      description: 'Error message to display',
    },
  },
  args: {
    onSubmit: (email: string, password: string) => {
      console.log('Login submitted:', { email, password });
    },
  },
} satisfies Meta<typeof LoginForm>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default state of the login form.
 */
export const Default: Story = {
  args: {
    isLoading: false,
    error: undefined,
  },
};

/**
 * Login form in loading state while authentication is in progress.
 */
export const Loading: Story = {
  args: {
    isLoading: true,
    error: undefined,
  },
};

/**
 * Login form showing an error message after failed authentication.
 */
export const WithError: Story = {
  args: {
    isLoading: false,
    error: 'Invalid email or password. Please try again.',
  },
};

/**
 * Login form with pre-filled values for demonstration.
 */
export const WithValues: Story = {
  render: (args) => {
    return (
      <div className="w-full">
        <LoginForm {...args} />
        <div className="mt-4 p-4 bg-gray-100 rounded-md text-sm text-gray-600">
          <p className="font-semibold">Storybook Note:</p>
          <p>This form is interactive. Enter email and password to see validation.</p>
          <p>Check the Actions panel below to see form submission events.</p>
        </div>
      </div>
    );
  },
  args: {
    isLoading: false,
    error: undefined,
  },
};
