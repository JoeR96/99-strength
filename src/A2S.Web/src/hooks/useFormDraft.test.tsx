import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { act, fireEvent, render, renderHook, screen } from '@testing-library/react';
import { createMemoryRouter, Link, RouterProvider } from 'react-router-dom';
import { useFormDraft, readFormDraft } from './useFormDraft';
import { UnsavedChangesGuard } from '@/components/shared/UnsavedChangesGuard';
import { DraftRestorePrompt } from '@/components/shared/DraftRestorePrompt';
import { describeSavedAt } from '@/components/shared/draftTime';

let store = new Map<string, string>();

beforeEach(() => {
  store = new Map<string, string>();
  vi.mocked(localStorage.getItem).mockImplementation((key: string) => store.get(key) ?? null);
  vi.mocked(localStorage.setItem).mockImplementation((key: string, value: string) => void store.set(key, value));
  vi.mocked(localStorage.removeItem).mockImplementation((key: string) => void store.delete(key));
  vi.useFakeTimers({ shouldAdvanceTime: true });
  vi.setSystemTime(new Date('2026-11-22T19:30:00.000Z'));
});

afterEach(() => vi.useRealTimers());

const initial = { name: 'Mobility A', minutes: '10' };

describe('useFormDraft', () => {
  it('saves a versioned draft while the form differs, and removes it when the form is back to the start', () => {
    const { rerender } = renderHook(({ value }) => useFormDraft({ key: 'routine:r1', version: 1, value, initial }), { initialProps: { value: initial } });
    expect(store.has('a2s-draft:routine:r1')).toBe(false);

    rerender({ value: { name: 'Mobility A+', minutes: '10' } });
    expect(JSON.parse(store.get('a2s-draft:routine:r1')!)).toEqual({
      version: 1,
      savedAt: '2026-11-22T19:30:00.000Z',
      value: { name: 'Mobility A+', minutes: '10' },
    });

    rerender({ value: initial });
    expect(store.has('a2s-draft:routine:r1')).toBe(false);
  });

  it('offers a saved draft on reopening and keeps it until Restore or Discard', () => {
    store.set('a2s-draft:routine:r1', JSON.stringify({ version: 1, savedAt: '2026-11-22T18:00:00.000Z', value: { name: 'Draft', minutes: '12' } }));

    const { result } = renderHook(() => useFormDraft({ key: 'routine:r1', version: 1, value: initial, initial }));

    expect(result.current.pendingDraft).toEqual({ savedAt: '2026-11-22T18:00:00.000Z', value: { name: 'Draft', minutes: '12' } });
    expect(store.has('a2s-draft:routine:r1')).toBe(true);
    let restored: typeof initial | undefined;
    act(() => {
      restored = result.current.restore();
    });
    expect(restored).toEqual({ name: 'Draft', minutes: '12' });
    expect(result.current.pendingDraft).toBeNull();
  });

  it('Discard removes the draft', () => {
    store.set('a2s-draft:routine:r1', JSON.stringify({ version: 1, savedAt: '2026-11-22T18:00:00.000Z', value: { name: 'Draft', minutes: '12' } }));
    const { result } = renderHook(() => useFormDraft({ key: 'routine:r1', version: 1, value: initial, initial }));

    act(() => result.current.discard());

    expect(result.current.pendingDraft).toBeNull();
    expect(store.has('a2s-draft:routine:r1')).toBe(false);
  });

  it('clear() after a save stops further writes', () => {
    const { result, rerender } = renderHook(({ value }) => useFormDraft({ key: 'routine:r1', version: 1, value, initial }), {
      initialProps: { value: { name: 'Changed', minutes: '10' } },
    });
    act(() => result.current.clear());
    rerender({ value: { name: 'Changed again', minutes: '10' } });

    expect(store.has('a2s-draft:routine:r1')).toBe(false);
  });

  it('drops a draft saved under another version, or one it cannot read', () => {
    store.set('a2s-draft:a', JSON.stringify({ version: 0, savedAt: 'x', value: {} }));
    store.set('a2s-draft:b', '{nope');

    expect(readFormDraft('a', 1)).toBeNull();
    expect(readFormDraft('b', 1)).toBeNull();
    expect(store.has('a2s-draft:a')).toBe(false);
  });
});

describe('DraftRestorePrompt', () => {
  it('says when the draft was saved, in London time', () => {
    const now = new Date('2026-11-22T21:00:00.000Z');
    expect(describeSavedAt('2026-11-22T19:30:00.000Z', now)).toBe('today at 19:30');
    expect(describeSavedAt('2026-07-01T07:05:00.000Z', now)).toBe('Wed 1 Jul at 08:05');
  });

  it('calls Restore and Discard', () => {
    const onRestore = vi.fn();
    const onDiscard = vi.fn();
    render(<DraftRestorePrompt savedAt="2026-11-22T19:30:00.000Z" onRestore={onRestore} onDiscard={onDiscard} />);

    expect(screen.getByRole('heading', { name: 'Restore unsaved changes?' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Restore' }));
    fireEvent.click(screen.getByRole('button', { name: 'Discard' }));
    expect(onRestore).toHaveBeenCalledOnce();
    expect(onDiscard).toHaveBeenCalledOnce();
  });
});

describe('UnsavedChangesGuard', () => {
  function renderWithRouter(dirty: boolean) {
    const router = createMemoryRouter(
      [
        {
          path: '/edit',
          element: (
            <>
              <UnsavedChangesGuard when={dirty} />
              <Link to="/list">Back</Link>
            </>
          ),
        },
        { path: '/list', element: <p>List page</p> },
      ],
      { initialEntries: ['/edit'] }
    );
    render(<RouterProvider router={router} />);
    return router;
  }

  it('asks before leaving with unsaved changes; Stay keeps the page, Leave goes', async () => {
    renderWithRouter(true);

    fireEvent.click(screen.getByRole('link', { name: 'Back' }));
    expect(await screen.findByRole('dialog', { name: 'Leave without saving?' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Stay' }));
    expect(screen.queryByText('List page')).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('link', { name: 'Back' }));
    fireEvent.click(await screen.findByRole('button', { name: 'Leave' }));
    expect(await screen.findByText('List page')).toBeInTheDocument();
  });

  it('lets a clean form go without asking', async () => {
    renderWithRouter(false);

    fireEvent.click(screen.getByRole('link', { name: 'Back' }));

    expect(await screen.findByText('List page')).toBeInTheDocument();
  });

  it('asks the browser before a reload with unsaved changes', () => {
    renderWithRouter(true);
    const event = new Event('beforeunload', { cancelable: true });

    window.dispatchEvent(event);

    expect(event.defaultPrevented).toBe(true);
  });
});
