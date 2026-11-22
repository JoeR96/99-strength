/**
 * Settings → "Push routines into Hevy" (on by default). Off stops the app writing planned routines into
 * Hevy; finished sessions (gym mode, walks, runs, home routines) are still posted to Hevy.
 */

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { usersApi, type UserPreferencesDto } from '@/api/users';

const preferencesKey = ['users', 'me', 'preferences'] as const;

export function RoutinePushSetting() {
  const queryClient = useQueryClient();
  const query = useQuery({ queryKey: preferencesKey, queryFn: () => usersApi.getPreferences() });
  const save = useMutation({
    mutationFn: (preferences: UserPreferencesDto) => usersApi.setPreferences(preferences),
    onSuccess: (saved) => queryClient.setQueryData(preferencesKey, saved),
    onError: () => toast.error('Could not save the setting'),
  });

  if (!query.data) return null;
  const on = save.variables?.pushRoutinesToHevy ?? query.data.pushRoutinesToHevy;

  return (
    <Card>
      <CardHeader>
        <CardTitle>Hevy routines</CardTitle>
        <CardDescription>Finished sessions are always sent to Hevy. This only controls the planned routines.</CardDescription>
      </CardHeader>
      <CardContent>
        <label className="flex min-h-12 items-center justify-between gap-4 text-foreground">
          <span>Push routines into Hevy</span>
          <input
            type="checkbox"
            role="switch"
            aria-checked={on}
            className="h-6 w-11 accent-primary"
            checked={on}
            disabled={save.isPending}
            onChange={(e) => save.mutate({ pushRoutinesToHevy: e.target.checked })}
          />
        </label>
      </CardContent>
    </Card>
  );
}
