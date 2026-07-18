/**
 * Hevy Settings Component
 * Allows users to configure their Hevy API key
 */

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { useHevy } from '@/contexts/HevyContext';

export function HevySettings() {
  const { apiKey, isConfigured, isValidating, isValid, setApiKey, clearApiKey } = useHevy();
  const [inputKey, setInputKey] = useState('');
  const [showKey, setShowKey] = useState(false);

  const handleSave = async () => {
    if (inputKey.trim()) {
      const success = await setApiKey(inputKey.trim());
      if (success) {
        setInputKey('');
      }
    }
  };

  const handleClear = () => {
    clearApiKey();
    setInputKey('');
  };

  const maskedKey = apiKey ? `${apiKey.slice(0, 8)}...${apiKey.slice(-4)}` : '';

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <svg className="h-5 w-5 text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
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
            className="text-primary hover:underline"
          >
            Hevy Pro subscription
          </a>
          .
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {isConfigured ? (
          <div className="space-y-4">
            <div className="flex items-center gap-3 p-3 rounded-lg bg-muted/30 border border-border/50">
              <div className={`h-3 w-3 rounded-full ${isValid ? 'bg-green-500' : isValid === false ? 'bg-red-500' : 'bg-yellow-500'}`} />
              <div className="flex-1">
                <p className="text-sm font-medium">
                  {isValidating ? 'Validating...' : isValid ? 'Connected' : isValid === false ? 'Invalid API Key' : 'Checking...'}
                </p>
                <p className="text-xs text-muted-foreground font-mono">
                  {showKey ? apiKey : maskedKey}
                </p>
              </div>
              <Button
                variant="ghost"
                size="sm"
                onClick={() => setShowKey(!showKey)}
                className="h-8 w-8 p-0"
              >
                {showKey ? (
                  <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13.875 18.825A10.05 10.05 0 0112 19c-4.478 0-8.268-2.943-9.543-7a9.97 9.97 0 011.563-3.029m5.858.908a3 3 0 114.243 4.243M9.878 9.878l4.242 4.242M9.88 9.88l-3.29-3.29m7.532 7.532l3.29 3.29M3 3l3.59 3.59m0 0A9.953 9.953 0 0112 5c4.478 0 8.268 2.943 9.543 7a10.025 10.025 0 01-4.132 5.411m0 0L21 21" />
                  </svg>
                ) : (
                  <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                  </svg>
                )}
              </Button>
            </div>
            <Button variant="outline" onClick={handleClear} className="w-full">
              Disconnect Hevy
            </Button>
          </div>
        ) : (
          <div className="space-y-4">
            <div className="space-y-2">
              <label className="text-sm font-medium">API Key</label>
              <Input
                type="password"
                value={inputKey}
                onChange={(e) => setInputKey(e.target.value)}
                placeholder="Enter your Hevy API key"
                disabled={isValidating}
              />
              <p className="text-xs text-muted-foreground">
                Get your API key from{' '}
                <a
                  href="https://hevy.com/settings?developer"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-primary hover:underline"
                >
                  Hevy Settings
                </a>
              </p>
            </div>
            <Button
              onClick={handleSave}
              disabled={!inputKey.trim() || isValidating}
              className="w-full"
            >
              {isValidating ? (
                <>
                  <svg className="h-4 w-4 mr-2 animate-spin" fill="none" viewBox="0 0 24 24">
                    <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                    <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
                  </svg>
                  Validating...
                </>
              ) : (
                'Connect Hevy'
              )}
            </Button>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
