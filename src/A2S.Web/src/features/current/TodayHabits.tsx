/**
 * Habits are the one daily thing on the Current workout page: compact counters, ticks and the
 * weigh-in (while a weigh-in habit is active), headed with today's date.
 */

import toast from 'react-hot-toast';
import { RefreshCw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { formatDayLabel, type LocalDate } from '@/lib/localDate';
import { useHabitsToday, useIncrementHabit, useSetHabitValue } from '@/hooks/useHabits';
import { useRecordWeight, useWeightTrend } from '@/hooks/useBodyWeight';
import type { HabitTodayDto } from '@/types/habits';
import { HabitCounter } from '@/features/today/HabitCounter';
import { HabitTick } from '@/features/today/HabitTick';
import { WeighInField } from '@/features/today/WeighInField';

function isWeighInHabit(habit: HabitTodayDto): boolean {
  return habit.type === 'Numeric' && (habit.unit ?? '').toLowerCase() === 'kg';
}

export function TodayHabits({ date }: { date: LocalDate }) {
  const habitsQuery = useHabitsToday(date);
  const trendQuery = useWeightTrend(date, date);
  const increment = useIncrementHabit();
  const setValue = useSetHabitValue();
  const recordWeight = useRecordWeight();
  const habits = habitsQuery.data;

  const weighInHabit = habits?.habits.find(isWeighInHabit) ?? null;
  const counters = habits?.habits.filter((h) => h.type === 'Counter') ?? [];
  const ticks = habits?.habits.filter((h) => h.type === 'Checkbox') ?? [];
  const todayPoint = trendQuery.data?.points.find((p) => p.date === date);
  const weighInKg = todayPoint?.kg ?? (weighInHabit && weighInHabit.value > 0 ? weighInHabit.value : null);

  const saveWeight = (kg: number) => {
    recordWeight.mutate(
      { date, kg },
      {
        onSuccess: () => {
          if (weighInHabit) setValue.set(weighInHabit.id, kg, date);
          toast.success(`Logged ${kg.toFixed(1)} kg`);
        },
        onError: () => toast.error("Couldn't save your weigh-in. Try again."),
      }
    );
  };

  return (
    <section aria-labelledby="habits-heading" className="space-y-2">
      <h2 id="habits-heading" className="text-section">
        Habits · {formatDayLabel(date)}
      </h2>
      {habitsQuery.isLoading && <div className="h-24 animate-pulse rounded-lg bg-muted" aria-hidden="true" />}
      {habitsQuery.isError && (
        <div className="rounded-lg border border-destructive/50 bg-card p-4" role="alert">
          <p className="text-sm text-foreground">Couldn't load habits.</p>
          <Button variant="outline" className="mt-3 h-11" onClick={() => void habitsQuery.refetch()}>
            <RefreshCw aria-hidden="true" />
            Try again
          </Button>
        </div>
      )}
      {habits && habits.habits.length === 0 && (
        <p className="rounded-lg border border-dashed border-border p-4 text-sm text-muted-foreground">
          No habits yet. Add water, walks and the rest on the Habits tab.
        </p>
      )}
      {counters.map((habit) => (
        <HabitCounter key={habit.id} habit={habit} onTap={increment.tap} />
      ))}
      {ticks.length > 0 && (
        <div className="grid grid-cols-2 gap-2">
          {ticks.map((habit) => (
            <HabitTick key={habit.id} habit={habit} onToggle={(id, done) => setValue.set(id, done ? 1 : 0, date)} />
          ))}
        </div>
      )}
      {!habitsQuery.isLoading && !habitsQuery.isError && weighInHabit && (
        <WeighInField valueKg={weighInKg} onSave={saveWeight} saving={recordWeight.isPending} />
      )}
    </section>
  );
}
