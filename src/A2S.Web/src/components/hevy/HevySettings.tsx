/**
 * Hevy Settings Component
 * Lets the user store, replace or remove their Hevy API key. The key is only ever sent to our
 * server (which encrypts it); the UI shows just its last four characters afterwards.
 */

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { useHevy } from '@/contexts/HevyContext';

export function HevySettings() {
  const { isConfigured, keyLastFour, isValidating, isValid, setApiKey, clearApiKey } = useHevy();
  const [inputKey, setInputKey] = useState('');
  const [replacing, setReplacing] = useState(false);

  const showInput = !isConfigured || replacing;

  const handleSave = async () => {
    if (inputKey.trim()) {
      const success = await setApiKey(inputKey.trim());
      if (success) {
        setInputKey('');
        setReplacing(false);
      }
    }
  };

  const handleRemove = async () => {
    await clearApiKey();
    setInputKey('');
    setReplacing(false);
  };

  const statusLabel = isValidating
    ? 'Validating...'
    : isValid
      ? 'Connected'
      : isValid === false
        ? 'Invalid API key'
        : 'Saved, not yet verified';

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <svg className="h-5 w-5 text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13.828 10.172a4 4 0 00-5.656 0l-4 4a4 4 0 105.656 5.656l1.102-1.101m-.758-4.899a4 4 0 005.656 0l4-4a4 4 0 00-5.656-5.656l-1.1 1.1" />
          </svg>
          Hevy Integration
        </CardTitle>
        <CardDescription>
          Connect your Hevy account to sync workouts. Requires a{' '}
          <a
            href="https://hevy.com/settings?developer"
            target="_blank"
            rel="noopener noreferrer"
            className="text-primary underline"
          >
            Hevy Pro subscription
          </a>
          . Your key is encrypted on our server and never shown again.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {isConfigured && (
          <div className="flex items-center gap-3 p-3 rounded-lg bg-muted/30 border border-border/50">
            <div
              className={`h-3 w-3 rounded-full ${isValid ? 'bg-success' : isValid === false ? 'bg-destructive' : 'bg-warning'}`}
              aria-hidden="true"
            />
            <div className="flex-1 min-w-0">
              <p className="text-sm font-medium">{statusLabel}</p>
              <p className="text-xs text-muted-foreground font-mono" data-testid="hevy-key-masked">
                {`••••••••${keyLastFour ?? ''}`}
              </p>
            </div>
          </div>
        )}

        {showInput && (
          <div className="space-y-2">
            <label htmlFor="hevy-api-key" className="text-sm font-medium">
              {isConfigured ? 'New API key' : 'API key'}
            </label>
            <Input
              id="hevy-api-key"
              type="password"
              autoComplete="off"
              value={inputKey}
              onChange={(e) => setInputKey(e.target.value)}
              placeholder="Paste your Hevy API key"
              disabled={isValidating}
            />
            <p className="text-xs text-muted-foreground">
              Get your API key from{' '}
              <a
                href="https://hevy.com/settings?developer"
                target="_blank"
                rel="noopener noreferrer"
                className="text-primary underline"
              >
                Hevy Settings
              </a>
            </p>
          </div>
        )}

        <div className="flex flex-col gap-2 sm:flex-row">
          {showInput && (
            <Button
              onClick={handleSave}
              disabled={!inputKey.trim() || isValidating}
              className="w-full min-h-11"
            >
              {isValidating ? 'Validating...' : isConfigured ? 'Save new key' : 'Connect Hevy'}
            </Button>
          )}
          {isConfigured && !replacing && (
            <Button variant="outline" onClick={() => setReplacing(true)} className="w-full min-h-11">
              Replace key
            </Button>
          )}
          {isConfigured && replacing && (
            <Button variant="ghost" onClick={() => { setReplacing(false); setInputKey(''); }} className="w-full min-h-11">
              Cancel
            </Button>
          )}
          {isConfigured && (
            <Button variant="destructive" onClick={handleRemove} className="w-full min-h-11">
              Remove key
            </Button>
          )}
        </div>
      </CardContent>
    </Card>
  );
}
