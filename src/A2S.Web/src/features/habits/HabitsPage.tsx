/**
 * Habits: today's values with quick controls, the streak, twelve weeks of
 * history per habit, and create / edit / archive.
 */

import { useMemo, useState } from 'react';
import { RefreshCw } from 'lucide-react';
import toast from 'react-hot-toast';
import { Navbar } from '@/components/layout/Navbar';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import { useLocalToday } from '@/hooks/useLocalToday';
import {
  useArchiveHabit,
  useCreateHabit,
  useHabitHistory,
  useHabits,
  useHabitsToday,
  useIncrementHabit,
  useSetHabitValue,
  useUpdateHabit,
} from '@/hooks/useHabits';
import type { CreateHabitRequest, HabitDto } from '@/types/habits';
import { StreakChip } from '@/features/today/StreakChip';
import { HabitList } from './HabitList';
import { HabitHeatmap } from './HabitHeatmap';
import { HabitEditSheet } from './HabitEditSheet';
import { NewHabitButton } from './NewHabitButton';
import { heatmapStart } from './heatmap';
import { toUpdateRequest } from './habitForm';

type SheetState = { mode: 'closed' } | { mode: 'create' } | { mode: 'edit'; habit: HabitDto };

export function HabitsPage() {
  const date = useLocalToday();
  const todayQuery = useHabitsToday(date);
  const definitionsQuery = useHabits(true);
  const historyFrom = heatmapStart(date);
  const historyQuery = useHabitHistory(historyFrom, date);
  const increment = useIncrementHabit();
  const setValue = useSetHabitValue();
  const createHabit = useCreateHabit();
  const updateHabit = useUpdateHabit();
  const archiveHabit = useArchiveHabit();

  const [sheet, setSheet] = useState<SheetState>({ mode: 'closed' });
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const habits = todayQuery.data?.habits ?? [];
  const definitions = useMemo(() => definitionsQuery.data ?? [], [definitionsQuery.data]);
  const active = useMemo(() => definitions.filter((d) => !d.archived), [definitions]);
  const archived = definitions.filter((d) => d.archived);
  const selected = useMemo(() => active.find((d) => d.id === selectedId) ?? active[0] ?? null, [active, selectedId]);

  const openEdit = (habitId: string) => {
    const habit = definitions.find((d) => d.id === habitId);
    if (habit) setSheet({ mode: 'edit', habit });
  };

  const save = (request: Omit<CreateHabitRequest, 'order'>, habit: HabitDto | null) => {
    if (habit) {
      updateHabit.mutate(
        { id: habit.id, request: toUpdateRequest(request, habit.order) },
        {
          onSuccess: () => {
            setSheet({ mode: 'closed' });
            toast.success(`${request.name} updated`);
          },
          onError: () => toast.error("Couldn't save the habit. Try again."),
        }
      );
    } else {
      const order = definitions.reduce((max, d) => Math.max(max, d.order), 0) + 1;
      createHabit.mutate(
        { ...request, order },
        {
          onSuccess: () => {
            setSheet({ mode: 'closed' });
            toast.success(`${request.name} added`);
          },
          onError: () => toast.error("Couldn't add the habit. Try again."),
        }
      );
    }
  };

  const archive = (habit: HabitDto, shouldArchive: boolean) => {
    archiveHabit.mutate(
      { id: habit.id, archive: shouldArchive },
      {
        onSuccess: () => {
          setSheet({ mode: 'closed' });
          toast.success(shouldArchive ? `${habit.name} archived` : `${habit.name} restored`);
        },
        onError: () => toast.error("Couldn't change that habit. Try again."),
      }
    );
  };

  const saving = createHabit.isPending || updateHabit.isPending || archiveHabit.isPending;

  return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <main className="container-page space-y-4 py-4">
        <header className="flex items-center justify-between gap-3">
          <h1 className="text-2xl font-bold text-foreground">Habits</h1>
          {todayQuery.data && <StreakChip streak={todayQuery.data.streak} />}
        </header>

        <section aria-labelledby="habits-today-heading" className="space-y-2">
          <h2 id="habits-today-heading" className="text-sm font-medium text-muted-foreground">
            Today
          </h2>
          {todayQuery.isLoading && <div className="h-40 animate-pulse rounded-lg bg-muted" aria-hidden="true" />}
          {todayQuery.isError && (
            <div className="rounded-lg border border-destructive/50 bg-card p-4" role="alert">
              <p className="text-sm text-foreground">Couldn't load habits.</p>
              <Button variant="outline" className="mt-3 h-11" onClick={() => void todayQuery.refetch()}>
                <RefreshCw aria-hidden="true" />
                Try again
              </Button>
            </div>
          )}
          {todayQuery.data && (
            <HabitList habits={habits} onTap={increment.tap} onSetValue={(id, value) => setValue.set(id, value, date)} onEdit={openEdit} />
          )}
          <NewHabitButton onClick={() => setSheet({ mode: 'create' })} />
        </section>

        {active.length > 0 && (
          <section aria-labelledby="habits-history-heading" className="space-y-2">
            <h2 id="habits-history-heading" className="text-sm font-medium text-muted-foreground">
              Last 12 weeks
            </h2>
            <div className="flex gap-2 overflow-x-auto pb-1" role="tablist" aria-label="Choose a habit">
              {active.map((habit) => (
                <button
                  key={habit.id}
                  type="button"
                  role="tab"
                  aria-selected={selected?.id === habit.id}
                  onClick={() => setSelectedId(habit.id)}
                  className={cn(
                    'h-11 shrink-0 rounded-full border px-4 text-sm font-medium',
                    selected?.id === habit.id ? 'border-primary bg-primary/10 text-foreground' : 'border-border text-muted-foreground'
                  )}
                >
                  {habit.name}
                </button>
              ))}
            </div>
            {historyQuery.isError && (
              <p className="text-sm text-muted-foreground" role="alert">
                Couldn't load history.
              </p>
            )}
            {selected && !historyQuery.isError && <HabitHeatmap habit={selected} history={historyQuery.data} to={date} />}
          </section>
        )}

        {archived.length > 0 && (
          <details className="rounded-lg border border-border bg-card p-4">
            <summary className="cursor-pointer text-sm font-medium text-muted-foreground">Archived ({archived.length})</summary>
            <ul className="mt-3 space-y-2">
              {archived.map((habit) => (
                <li key={habit.id} className="flex items-center justify-between gap-3 text-sm">
                  <span className="text-muted-foreground">{habit.name}</span>
                  <Button variant="outline" size="sm" className="h-11" onClick={() => archive(habit, false)}>
                    Restore
                  </Button>
                </li>
              ))}
            </ul>
          </details>
        )}
      </main>

      {sheet.mode !== 'closed' && (
        <HabitEditSheet
          key={sheet.mode === 'edit' ? sheet.habit.id : 'create'}
          open
          habit={sheet.mode === 'edit' ? sheet.habit : null}
          onClose={() => setSheet({ mode: 'closed' })}
          onSave={save}
          onArchive={archive}
          saving={saving}
        />
      )}
    </div>
  );
}
