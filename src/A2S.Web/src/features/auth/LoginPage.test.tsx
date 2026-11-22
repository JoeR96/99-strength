/** Pins defect 15 of the live walkthrough: the sign-in screens describe v2, which opens on Today. */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { LoginPage } from './LoginPage';
import { SignUpPage } from './SignUpPage';

vi.mock('@clerk/clerk-react', () => ({
  SignIn: () => <div data-testid="clerk-sign-in" />,
  SignUp: () => <div data-testid="clerk-sign-up" />,
}));

const V1_WORDING = /dashboard|powered by|average to savage|strength journey/i;

describe('sign-in screens copy', () => {
  it('sign-in says what v2 is and where it opens', () => {
    const { container } = render(<LoginPage />);

    expect(screen.getByRole('heading', { name: '99 Strength' })).toBeInTheDocument();
    expect(screen.getByText('Your whole training plan in one place')).toBeInTheDocument();
    expect(screen.getByText("Sign in to see what's on today")).toBeInTheDocument();
    expect(screen.getByText('Lifts, cardio, mobility, habits and weight')).toBeInTheDocument();
    expect(screen.getByTestId('clerk-sign-in')).toBeInTheDocument();
    expect(container.textContent).not.toMatch(V1_WORDING);
  });

  it('sign-up carries no v1 wording', () => {
    const { container } = render(<SignUpPage />);

    expect(screen.getByText('Create your account to get started')).toBeInTheDocument();
    expect(screen.getByTestId('clerk-sign-up')).toBeInTheDocument();
    expect(container.textContent).not.toMatch(V1_WORDING);
  });

  it.each([
    ['sign-in', <LoginPage key="in" />],
    ['sign-up', <SignUpPage key="up" />],
  ])('%s fits the phone: visible-viewport height, safe areas, no leftover gold theme', (_name, page) => {
    const { container } = render(page);
    const frame = screen.getByTestId('auth-layout');

    expect(frame.className).toContain('min-h-dvh');
    expect(frame.className).not.toContain('min-h-screen');
    expect(frame.className).toContain('safe-area-inset-top');
    expect(frame.className).toContain('safe-area-inset-bottom');
    expect(container.innerHTML).not.toMatch(/gold|navy|255,\s*212,\s*10/);
  });
});
