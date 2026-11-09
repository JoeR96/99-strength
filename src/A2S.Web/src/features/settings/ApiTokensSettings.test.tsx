import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';
import toast from 'react-hot-toast';
import { apiTokensApi } from '@/api/apiTokens';
import { apiTokenHandlers, installApiTokenHandlers } from '@/mocks/handlers/apiTokens';
import { createdTokenFixture, importToken, revokedToken, STORY_TOKEN, terranToken } from '@/mocks/fixtures/apiTokens';
import { ApiTokensSettings } from './ApiTokensSettings';

vi.mock('react-hot-toast', () => ({ default: { success: vi.fn(), error: vi.fn() } }));

function wrapper({ children }: { children: ReactNode }) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } } });
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

function stubClipboard(writeText: (text: string) => Promise<void>) {
  Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
}

describe('ApiTokensSettings', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    installApiTokenHandlers(apiTokenHandlers.empty());
  });

  it('with no tokens: says so, and Create stays off until there is a name', async () => {
    render(<ApiTokensSettings />, { wrapper });

    expect(await screen.findByTestId('api-tokens-empty')).toHaveTextContent('No tokens yet.');
    const create = screen.getByRole('button', { name: /create token/i });
    expect(create).toBeDisabled();

    fireEvent.change(screen.getByLabelText(/token name/i), { target: { value: '   ' } });
    expect(create).toBeDisabled();

    fireEvent.change(screen.getByLabelText(/token name/i), { target: { value: 'Terran CC' } });
    expect(create).toBeEnabled();
  });

  it('limits the name to 50 characters', () => {
    render(<ApiTokensSettings />, { wrapper });

    expect(screen.getByLabelText(/token name/i)).toHaveAttribute('maxLength', '50');
  });

  it('creating shows the token once in a read-only field, with the warning, and lists it', async () => {
    const createSpy = vi.spyOn(apiTokensApi, 'create');
    render(<ApiTokensSettings />, { wrapper });
    await screen.findByTestId('api-tokens-empty');

    fireEvent.change(screen.getByLabelText(/token name/i), { target: { value: '  Terran CC  ' } });
    fireEvent.click(screen.getByRole('button', { name: /create token/i }));

    const panel = await screen.findByTestId('api-token-created');
    expect(createSpy).toHaveBeenCalledWith('Terran CC');
    const value = within(panel).getByLabelText(/new token for terran cc/i);
    expect(value).toHaveValue(STORY_TOKEN);
    expect(value).toHaveAttribute('readonly');
    expect(within(panel).getByRole('alert')).toHaveTextContent(/will not be shown again/i);
    expect(screen.getByLabelText(/token name/i)).toHaveValue('');

    const list = await screen.findByTestId('api-tokens-list');
    expect(within(list).getByText('Terran CC')).toBeInTheDocument();
    expect(within(list).getByText(/never used/i)).toBeInTheDocument();
    expect(list).not.toHaveTextContent(STORY_TOKEN);
  });

  it('Done puts the new token away for good', async () => {
    installApiTokenHandlers(apiTokenHandlers.withTokens());
    render(<ApiTokensSettings justCreated={createdTokenFixture} />, { wrapper });

    fireEvent.click(await screen.findByRole('button', { name: /done/i }));

    expect(screen.queryByTestId('api-token-created')).not.toBeInTheDocument();
    expect(document.body).not.toHaveTextContent(STORY_TOKEN);
  });

  it('Copy puts the token on the clipboard', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    stubClipboard(writeText);
    render(<ApiTokensSettings justCreated={createdTokenFixture} />, { wrapper });

    fireEvent.click(screen.getByRole('button', { name: /copy/i }));

    await waitFor(() => expect(toast.success).toHaveBeenCalledWith('Token copied'));
    expect(writeText).toHaveBeenCalledWith(STORY_TOKEN);
  });

  it('when the clipboard refuses, it says to copy by hand', async () => {
    stubClipboard(vi.fn().mockRejectedValue(new Error('denied')));
    render(<ApiTokensSettings justCreated={createdTokenFixture} />, { wrapper });

    fireEvent.click(screen.getByRole('button', { name: /copy/i }));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith(expect.stringMatching(/couldn't copy/i)));
  });

  it('lists active tokens with prefix, created and last used; revoked ones are left out', async () => {
    installApiTokenHandlers(apiTokenHandlers.withTokens());
    render(<ApiTokensSettings />, { wrapper });

    const list = await screen.findByTestId('api-tokens-list');
    const rows = within(list).getAllByRole('listitem');
    expect(rows).toHaveLength(2);
    expect(rows[0]).toHaveTextContent(importToken.name);
    expect(rows[0]).toHaveTextContent('nns_pat_q7Lm…');
    expect(rows[0]).toHaveTextContent('Created 8 Nov 2026');
    expect(rows[0]).toHaveTextContent('Never used');
    expect(rows[1]).toHaveTextContent(terranToken.name);
    expect(rows[1]).toHaveTextContent('nns_pat_Stor…');
    expect(rows[1]).toHaveTextContent('Created 2 Nov 2026');
    expect(rows[1]).toHaveTextContent(/Last used 9 Nov 2026/);
    expect(list).not.toHaveTextContent(revokedToken.name);
  });

  it('Revoke asks first: Keep changes nothing', async () => {
    installApiTokenHandlers(apiTokenHandlers.withTokens());
    const revokeSpy = vi.spyOn(apiTokensApi, 'revoke');
    render(<ApiTokensSettings />, { wrapper });

    fireEvent.click(await screen.findByRole('button', { name: 'Revoke Terran CC' }));
    const dialog = await screen.findByRole('alertdialog');
    expect(dialog).toHaveTextContent('Revoke Terran CC?');
    expect(dialog).toHaveTextContent(/stops working straight away/i);
    fireEvent.click(within(dialog).getByRole('button', { name: 'Keep' }));

    await waitFor(() => expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument());
    expect(revokeSpy).not.toHaveBeenCalled();
    expect(within(screen.getByTestId('api-tokens-list')).getAllByRole('listitem')).toHaveLength(2);
  });

  it('Revoke, once confirmed, revokes that token and drops it from the list', async () => {
    installApiTokenHandlers(apiTokenHandlers.withTokens());
    const revokeSpy = vi.spyOn(apiTokensApi, 'revoke');
    render(<ApiTokensSettings />, { wrapper });

    fireEvent.click(await screen.findByRole('button', { name: 'Revoke Terran CC' }));
    const dialog = await screen.findByRole('alertdialog');
    fireEvent.click(within(dialog).getByRole('button', { name: 'Revoke' }));

    await waitFor(() => expect(revokeSpy).toHaveBeenCalledWith(terranToken.id));
    await waitFor(() => expect(within(screen.getByTestId('api-tokens-list')).getAllByRole('listitem')).toHaveLength(1));
    expect(screen.getByTestId('api-tokens-list')).not.toHaveTextContent(terranToken.name);
    expect(toast.success).toHaveBeenCalledWith('Terran CC revoked');
  });

  it('at ten active tokens the form is closed and says why', async () => {
    installApiTokenHandlers(apiTokenHandlers.atCap());
    render(<ApiTokensSettings />, { wrapper });

    expect(await screen.findByTestId('api-tokens-limit')).toHaveTextContent(/revoke one to create another/i);
    expect(screen.getByLabelText(/token name/i)).toBeDisabled();
    expect(screen.getByRole('button', { name: /create token/i })).toBeDisabled();
  });

  it("shows the server's reason when creating fails", async () => {
    installApiTokenHandlers({
      ...apiTokenHandlers.empty(),
      create: () =>
        Promise.reject(Object.assign(new Error('422'), { response: { status: 422, data: { detail: 'You can have at most 10 active tokens.' } } })),
    });
    render(<ApiTokensSettings />, { wrapper });
    await screen.findByTestId('api-tokens-empty');

    fireEvent.change(screen.getByLabelText(/token name/i), { target: { value: 'Eleventh' } });
    fireEvent.click(screen.getByRole('button', { name: /create token/i }));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('You can have at most 10 active tokens.'));
    expect(screen.queryByTestId('api-token-created')).not.toBeInTheDocument();
    expect(screen.getByLabelText(/token name/i)).toHaveValue('Eleventh');
  });

  it('says so when the list cannot be loaded', async () => {
    installApiTokenHandlers(apiTokenHandlers.error());
    render(<ApiTokensSettings />, { wrapper });

    expect(await screen.findByRole('alert')).toHaveTextContent(/couldn't load your tokens/i);
  });
});
