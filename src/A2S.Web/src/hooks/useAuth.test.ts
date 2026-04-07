import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook } from '@testing-library/react';
import { useAuth } from './useAuth';

const mockClerkAuth = {
  isLoaded: true,
  isSignedIn: true,
  userId: 'user-123',
  getToken: vi.fn(),
};

vi.mock('@clerk/clerk-react', () => ({
  useAuth: () => mockClerkAuth,
}));

describe('useAuth', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockClerkAuth.isLoaded = true;
    mockClerkAuth.isSignedIn = true;
    mockClerkAuth.userId = 'user-123';
  });

  it('returns isLoading false when Clerk is loaded', () => {
    const { result } = renderHook(() => useAuth());
    expect(result.current.isLoading).toBe(false);
  });

  it('returns isLoading true when Clerk is not loaded', () => {
    mockClerkAuth.isLoaded = false;
    const { result } = renderHook(() => useAuth());
    expect(result.current.isLoading).toBe(true);
  });

  it('returns isAuthenticated true when signed in', () => {
    const { result } = renderHook(() => useAuth());
    expect(result.current.isAuthenticated).toBe(true);
  });

  it('returns isAuthenticated false when not signed in', () => {
    mockClerkAuth.isSignedIn = false;
    const { result } = renderHook(() => useAuth());
    expect(result.current.isAuthenticated).toBe(false);
  });

  it('returns isAuthenticated false when isSignedIn is null', () => {
    mockClerkAuth.isSignedIn = null as unknown as boolean;
    const { result } = renderHook(() => useAuth());
    expect(result.current.isAuthenticated).toBe(false);
  });

  it('returns userId from Clerk', () => {
    const { result } = renderHook(() => useAuth());
    expect(result.current.userId).toBe('user-123');
  });

  it('getAccessToken returns token when signed in', async () => {
    mockClerkAuth.getToken.mockResolvedValue('test-token');
    const { result } = renderHook(() => useAuth());
    const token = await result.current.getAccessToken();
    expect(token).toBe('test-token');
  });

  it('getAccessToken returns null when not signed in', async () => {
    mockClerkAuth.isSignedIn = false;
    const { result } = renderHook(() => useAuth());
    const token = await result.current.getAccessToken();
    expect(token).toBeNull();
  });
});
