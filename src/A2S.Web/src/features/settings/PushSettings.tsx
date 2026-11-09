/**
 * Notifications on Settings. Where push works: one button to turn nudges on
 * (permission → subscribe → tell the server), the time the "time to train"
 * nudge should arrive, and a way to turn them off. iOS Safari in a tab cannot
 * receive push, so it gets the Home Screen install steps instead.
 */

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Bell, BellOff, Share, SquarePlus } from 'lucide-react';
import toast from 'react-hot-toast';
import { pushApi } from '@/api/push';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { browserPushClient, type PushClient } from './pushClient';
import { detectPushSupport, readSupportEnv, type SupportEnv } from './pushSupport';
import { pushKeys, toTimeInput } from './pushKeys';

export interface PushSettingsProps {
  /** The browser environment; defaults to the real window (stories pass a fake). */
  env?: SupportEnv;
  /** Permission and subscription calls; defaults to the real browser APIs. */
  client?: PushClient;
}

export function PushSettings({ env, client = browserPushClient }: PushSettingsProps) {
  const support = detectPushSupport(env ?? readSupportEnv());

  return (
    <Card data-testid="push-settings">
      <CardHeader>
        <CardTitle>Notifications</CardTitle>
        <CardDescription>
          A nudge when it's time to train and, at 20:00, a note of any habit still under its minimum.
        </CardDescription>
      </CardHeader>
      <CardContent>
        {support === 'ios-needs-install' && <InstallSteps />}
        {support === 'unsupported' && (
          <p className="text-sm text-muted-foreground" data-testid="push-unsupported">
            This browser can't receive notifications from 99 Strength.
          </p>
        )}
        {support === 'supported' && <PushControls client={client} />}
      </CardContent>
    </Card>
  );
}

function InstallSteps() {
  return (
    <div data-testid="push-install-steps" className="space-y-3">
      <p className="text-sm text-foreground">
        On iPhone and iPad, notifications only work once 99 Strength is on your Home Screen.
      </p>
      <ol className="space-y-2 text-sm text-muted-foreground">
        <li className="flex items-center gap-2">
          <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-muted text-xs font-semibold text-foreground">1</span>
          Tap <Share className="h-4 w-4" aria-hidden="true" /> <span className="font-medium text-foreground">Share</span> in Safari.
        </li>
        <li className="flex items-center gap-2">
          <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-muted text-xs font-semibold text-foreground">2</span>
          Choose <SquarePlus className="h-4 w-4" aria-hidden="true" /> <span className="font-medium text-foreground">Add to Home Screen</span>.
        </li>
        <li className="flex items-center gap-2">
          <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-muted text-xs font-semibold text-foreground">3</span>
          Open 99 Strength from the Home Screen and come back here.
        </li>
      </ol>
    </div>
  );
}

function PushControls({ client }: { client: PushClient }) {
  const queryClient = useQueryClient();
  const vapid = useQuery({ queryKey: pushKeys.vapid(), queryFn: pushApi.getVapidPublicKey, staleTime: Infinity });
  const browser = useQuery({ queryKey: pushKeys.browser(), queryFn: () => client.getSubscription(), staleTime: Infinity });
  const server = useQuery({ queryKey: pushKeys.subscriptions(), queryFn: pushApi.listSubscriptions });

  const [permission, setPermission] = useState<NotificationPermission>(() => client.permission());
  const [time, setTime] = useState<string | null>(null);

  const refresh = () => Promise.all([
    queryClient.invalidateQueries({ queryKey: pushKeys.browser() }),
    queryClient.invalidateQueries({ queryKey: pushKeys.subscriptions() }),
  ]);

  const current = browser.data ?? null;
  const stored = current ? server.data?.find((s) => s.endpoint === current.endpoint) ?? null : null;
  const subscribed = current !== null && stored !== null;
  const timeValue = time ?? toTimeInput(stored?.sessionStartTime);

  const enable = useMutation({
    mutationFn: async () => {
      const key = vapid.data;
      if (!key) throw new Error('Push is not configured on the server.');
      const granted = await client.requestPermission();
      setPermission(granted);
      if (granted !== 'granted') throw new Error('Notifications were not allowed.');
      const subscription = (await client.getSubscription()) ?? (await client.subscribe(key));
      await pushApi.subscribe({
        endpoint: subscription.endpoint,
        p256dh: subscription.keys.p256dh,
        auth: subscription.keys.auth,
        userAgent: navigator.userAgent,
        sessionStartTime: timeValue,
      });
    },
    onSuccess: async () => {
      toast.success('Notifications on');
      await refresh();
    },
    onError: (error: Error) => toast.error(error.message || "Couldn't turn notifications on."),
  });

  const saveTime = useMutation({
    mutationFn: async () => {
      if (!current) return;
      await pushApi.subscribe({
        endpoint: current.endpoint,
        p256dh: current.keys.p256dh,
        auth: current.keys.auth,
        userAgent: navigator.userAgent,
        sessionStartTime: timeValue,
      });
    },
    onSuccess: async () => {
      toast.success(`Training nudge at ${timeValue}`);
      setTime(null);
      await refresh();
    },
    onError: () => toast.error("Couldn't save that time."),
  });

  const disable = useMutation({
    mutationFn: async () => {
      if (!current) return;
      await pushApi.unsubscribe(current.endpoint);
      await current.unsubscribe();
    },
    onSuccess: async () => {
      toast.success('Notifications off');
      await refresh();
    },
    onError: () => toast.error("Couldn't turn notifications off."),
  });

  if (vapid.isLoading || browser.isLoading || server.isLoading) {
    return <p className="text-sm text-muted-foreground">Checking…</p>;
  }

  if (vapid.isError || server.isError) {
    return (
      <p className="text-sm text-destructive" role="alert">
        Couldn't check notification settings. Try again later.
      </p>
    );
  }

  if (vapid.data === null) {
    return (
      <p className="text-sm text-muted-foreground" data-testid="push-not-configured">
        Notifications aren't set up on this server yet.
      </p>
    );
  }

  if (permission === 'denied') {
    return (
      <p className="text-sm text-muted-foreground" data-testid="push-denied">
        Notifications are blocked for 99 Strength. Allow them in your browser's site settings, then come back.
      </p>
    );
  }

  if (!subscribed) {
    return (
      <div className="space-y-3">
        <p className="text-sm text-muted-foreground">Nudges are off on this device.</p>
        <Button className="h-11" onClick={() => enable.mutate()} disabled={enable.isPending} data-testid="push-enable">
          <Bell aria-hidden="true" />
          {enable.isPending ? 'Turning on…' : 'Turn on notifications'}
        </Button>
      </div>
    );
  }

  return (
    <div className="space-y-4" data-testid="push-enabled">
      <p className="flex items-center gap-2 text-sm text-foreground">
        <Bell className="h-4 w-4 text-success" aria-hidden="true" />
        Notifications are on for this device.
      </p>
      <form
        className="flex flex-wrap items-end gap-3"
        onSubmit={(event) => {
          event.preventDefault();
          saveTime.mutate();
        }}
      >
        <div>
          <label htmlFor="push-session-start" className="text-sm font-medium text-foreground">
            Training nudge time
          </label>
          <Input
            id="push-session-start"
            type="time"
            value={timeValue}
            onChange={(event) => setTime(event.target.value)}
            className="mt-1 h-11 w-36"
          />
        </div>
        <Button type="submit" variant="outline" className="h-11" disabled={saveTime.isPending || time === null}>
          {saveTime.isPending ? 'Saving…' : 'Save time'}
        </Button>
      </form>
      <Button variant="ghost" className="h-11" onClick={() => disable.mutate()} disabled={disable.isPending} data-testid="push-disable">
        <BellOff aria-hidden="true" />
        Turn off notifications
      </Button>
    </div>
  );
}
