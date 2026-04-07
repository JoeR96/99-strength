import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { type ReactNode } from 'react';
import { ProtectedRoute } from './ProtectedRoute';

const mockSignedIn = vi.fn();
const mockSignedOut = vi.fn();

vi.mock('@clerk/clerk-react', () => ({
  SignedIn: ({ children }: { children: ReactNode }) => {
    mockSignedIn();
    return <>{children}</>;
  },
  SignedOut: ({ children }: { children: ReactNode }) => {
    mockSignedOut();
    return <>{children}</>;
  },
}));

describe('ProtectedRoute', () => {
  it('renders children within SignedIn', () => {
    render(
      <MemoryRouter>
        <ProtectedRoute>
          <div>Protected Content</div>
        </ProtectedRoute>
      </MemoryRouter>
    );
    expect(mockSignedIn).toHaveBeenCalled();
    expect(screen.getByText('Protected Content')).toBeInTheDocument();
  });

  it('includes SignedOut redirect', () => {
    render(
      <MemoryRouter>
        <ProtectedRoute>
          <div>Protected Content</div>
        </ProtectedRoute>
      </MemoryRouter>
    );
    expect(mockSignedOut).toHaveBeenCalled();
  });
});
