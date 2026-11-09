/**
 * "Desktop widget tokens" on Settings. A token lets a program that cannot sign in
 * (the Terran CC habit widget) read and log habits. Create one, copy it from the
 * read-only field (it is shown once), and revoke it from the list when it is no
 * longer needed.
 */

import { useRef, useState, type FormEvent } from 'react';
import { Copy, KeyRound } from 'lucide-react';
import toast from 'react-hot-toast';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { useConfirmDialog } from '@/hooks/useConfirmDialog';
import { useApiTokens, useCreateApiToken, useRevokeApiToken } from '@/hooks/useApiTokens';
import {
  API_TOKEN_MAX_ACTIVE,
  API_TOKEN_NAME_MAX_LENGTH,
  type ApiTokenDto,
  type CreatedApiTokenDto,
} from '@/types/apiTokens';

export interface ApiTokensSettingsProps {
  /** Start with a just-created token on show (stories and tests; the app never passes it). */
  justCreated?: CreatedApiTokenDto;
}

const dateFormat = new Intl.DateTimeFormat('en-GB', { day: 'numeric', month: 'short', year: 'numeric' });
const dateTimeFormat = new Intl.DateTimeFormat('en-GB', {
  day: 'numeric',
  month: 'short',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
});

/** The server's explanation when it sent one (a ProblemDetails `detail`), otherwise the fallback. */
function serverMessage(error: unknown, fallback: string): string {
  const data = (error as { response?: { data?: { detail?: string } } } | null)?.response?.data;
  return data?.detail || fallback;
}

export function ApiTokensSettings({ justCreated }: ApiTokensSettingsProps) {
  const tokens = useApiTokens();
  const create = useCreateApiToken();
  const [name, setName] = useState('');
  const [created, setCreated] = useState<CreatedApiTokenDto | null>(justCreated ?? null);

  const active = (tokens.data ?? []).filter((token) => token.revokedAt === null);
  const atLimit = active.length >= API_TOKEN_MAX_ACTIVE;
  const trimmed = name.trim();

  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (!trimmed || atLimit || create.isPending) return;
    create.mutate(trimmed, {
      onSuccess: (token) => {
        setCreated(token);
        setName('');
      },
      onError: (error) => toast.error(serverMessage(error, "Couldn't create the token. Try again.")),
    });
  };

  return (
    <Card data-testid="api-tokens-settings">
      <CardHeader>
        <CardTitle>Desktop widget tokens</CardTitle>
        <CardDescription>
          A token lets a program on your computer, such as the Terran CC habit widget, read and log your habits
          and weigh-ins without signing in. It can also read today's plan. It can't change anything else.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-6">
        <form className="flex flex-wrap items-end gap-3" onSubmit={submit}>
          <div className="min-w-0 flex-1 basis-48">
            <label htmlFor="api-token-name" className="text-sm font-medium text-foreground">
              Token name
            </label>
            <Input
              id="api-token-name"
              value={name}
              onChange={(event) => setName(event.target.value)}
              maxLength={API_TOKEN_NAME_MAX_LENGTH}
              placeholder="Terran CC"
              autoComplete="off"
              disabled={atLimit}
              className="mt-1"
            />
          </div>
          <Button type="submit" className="h-11" disabled={!trimmed || atLimit || create.isPending}>
            <KeyRound aria-hidden="true" />
            {create.isPending ? 'Creating…' : 'Create token'}
          </Button>
          {atLimit && (
            <p className="basis-full text-sm text-muted-foreground" data-testid="api-tokens-limit">
              You have {API_TOKEN_MAX_ACTIVE} tokens, which is the most you can have. Revoke one to create another.
            </p>
          )}
        </form>

        {created && <NewToken created={created} onDone={() => setCreated(null)} />}

        <TokenList tokens={active} isLoading={tokens.isLoading} isError={tokens.isError} />
      </CardContent>
    </Card>
  );
}

function NewToken({ created, onDone }: { created: CreatedApiTokenDto; onDone: () => void }) {
  const field = useRef<HTMLInputElement>(null);

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(created.token);
      toast.success('Token copied');
    } catch {
      field.current?.select();
      toast.error("Couldn't copy. Select the token and copy it yourself.");
    }
  };

  return (
    <div className="space-y-3 rounded-lg border border-primary/60 bg-muted/40 p-4" data-testid="api-token-created">
      <label htmlFor="api-token-value" className="block text-sm font-medium text-foreground">
        New token for {created.name}
      </label>
      <div className="flex flex-wrap gap-3">
        <Input
          id="api-token-value"
          ref={field}
          readOnly
          value={created.token}
          onFocus={(event) => event.currentTarget.select()}
          className="min-w-0 flex-1 basis-48 font-mono text-sm"
        />
        <Button type="button" variant="outline" className="h-11" onClick={copy}>
          <Copy aria-hidden="true" />
          Copy
        </Button>
      </div>
      <p className="text-sm text-foreground" role="alert">
        Copy this token now. It will not be shown again. If you lose it, revoke it and create a new one.
      </p>
      <Button type="button" variant="ghost" className="h-11" onClick={onDone}>
        Done
      </Button>
    </div>
  );
}

function TokenList({ tokens, isLoading, isError }: { tokens: ApiTokenDto[]; isLoading: boolean; isError: boolean }) {
  const revoke = useRevokeApiToken();
  const { confirm, ConfirmDialog } = useConfirmDialog();

  const askToRevoke = async (token: ApiTokenDto) => {
    const confirmed = await confirm({
      title: `Revoke ${token.name}?`,
      description: 'Anything using this token stops working straight away. This cannot be undone.',
      confirmLabel: 'Revoke',
      cancelLabel: 'Keep',
      variant: 'destructive',
    });
    if (!confirmed) return;
    revoke.mutate(token.id, {
      onSuccess: () => toast.success(`${token.name} revoked`),
      onError: (error) => toast.error(serverMessage(error, "Couldn't revoke the token. Try again.")),
    });
  };

  if (isLoading) {
    return <p className="text-sm text-muted-foreground">Loading tokens…</p>;
  }

  if (isError) {
    return (
      <p className="text-sm text-destructive" role="alert">
        Couldn't load your tokens. Try again later.
      </p>
    );
  }

  if (tokens.length === 0) {
    return (
      <p className="text-sm text-muted-foreground" data-testid="api-tokens-empty">
        No tokens yet.
      </p>
    );
  }

  return (
    <>
      <ul className="divide-y divide-border rounded-lg border border-border" data-testid="api-tokens-list">
        {tokens.map((token) => (
          <li key={token.id} className="flex flex-wrap items-center justify-between gap-3 p-4">
            <div className="min-w-0 flex-1 basis-48">
              <p className="break-words font-medium text-foreground">{token.name}</p>
              <p className="font-mono text-sm text-muted-foreground">{token.prefix}…</p>
              <p className="text-sm text-muted-foreground">
                Created {dateFormat.format(new Date(token.createdAt))}
                {' · '}
                {token.lastUsedAt ? `Last used ${dateTimeFormat.format(new Date(token.lastUsedAt))}` : 'Never used'}
              </p>
            </div>
            <Button
              type="button"
              variant="outline"
              className="h-11"
              onClick={() => askToRevoke(token)}
              disabled={revoke.isPending}
              aria-label={`Revoke ${token.name}`}
            >
              Revoke
            </Button>
          </li>
        ))}
      </ul>
      {ConfirmDialog}
    </>
  );
}
