/**
 * Progress: the weight trend against the plan's checkpoints, this week's hard
 * sets against their caps, and the checkpoints themselves. Workout history and
 * the Hevy charts stay one tap away.
 */

import { Link } from 'react-router-dom';
import { ChevronRight, History, LineChart, RefreshCw } from 'lucide-react';
import { Navbar } from '@/components/layout/Navbar';
import { Button } from '@/components/ui/button';
import { addDays } from '@/lib/localDate';
import { useLocalToday } from '@/hooks/useLocalToday';
import { useActivePlan } from '@/hooks/usePlan';
import { useWeightTrend } from '@/hooks/useBodyWeight';
import { useCurrentWorkout } from '@/hooks/useWorkouts';
import { useDayBudget } from '@/hooks/useDayBudget';
import { WeightTrendChart } from './WeightTrendChart';
import { VolumeThisWeek } from './VolumeThisWeek';
import { CheckpointsList } from './CheckpointsList';

const TREND_DAYS = 90;

export function ProgressPage() {
  const date = useLocalToday();
  const trendQuery = useWeightTrend(addDays(date, -(TREND_DAYS - 1)), date);
  const planQuery = useActivePlan();
  const workoutQuery = useCurrentWorkout();
  const workout = workoutQuery.data ?? null;
  const budgetQuery = useDayBudget(workout?.id, workout?.currentDay ?? 1, workout?.currentWeek);

  const plan = planQuery.data ?? null;

  return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <main className="container-page space-y-4 py-4">
        <header>
          <h1 className="text-2xl font-bold text-foreground">Progress</h1>
          {plan && <p className="text-sm text-muted-foreground">{plan.name}</p>}
        </header>

        {trendQuery.isLoading && <div className="h-72 animate-pulse rounded-lg bg-muted" aria-hidden="true" />}
        {trendQuery.isError && (
          <section className="rounded-lg border border-destructive/50 bg-card p-4" role="alert">
            <p className="text-sm text-foreground">Couldn't load your weight trend.</p>
            <Button variant="outline" className="mt-3 h-11" onClick={() => void trendQuery.refetch()}>
              <RefreshCw aria-hidden="true" />
              Try again
            </Button>
          </section>
        )}
        {trendQuery.data && (
          <WeightTrendChart
            trend={trendQuery.data}
            checkpoints={plan?.checkpoints ?? []}
            startDate={plan?.startDate}
            startKg={plan?.startWeightKg}
          />
        )}

        <VolumeThisWeek
          budget={budgetQuery.data}
          loading={workoutQuery.isLoading || budgetQuery.isLoading}
          error={budgetQuery.isError}
        />

        {plan && trendQuery.data && <CheckpointsList checkpoints={plan.checkpoints} points={trendQuery.data.points} today={date} />}

        <nav aria-label="More progress" className="grid gap-2">
          <Link
            to="/history"
            className="flex min-h-14 items-center gap-3 rounded-lg border border-border bg-card px-4 transition-colors hover:bg-muted"
          >
            <History className="h-5 w-5 text-muted-foreground" aria-hidden="true" />
            <span className="flex-1">
              <span className="block font-medium text-foreground">Workout history</span>
              <span className="block text-xs text-muted-foreground">Every completed day and what changed</span>
            </span>
            <ChevronRight className="h-5 w-5 text-muted-foreground" aria-hidden="true" />
          </Link>
          <Link
            to="/hevy/data"
            className="flex min-h-14 items-center gap-3 rounded-lg border border-border bg-card px-4 transition-colors hover:bg-muted"
          >
            <LineChart className="h-5 w-5 text-muted-foreground" aria-hidden="true" />
            <span className="flex-1">
              <span className="block font-medium text-foreground">Hevy data</span>
              <span className="block text-xs text-muted-foreground">Charts from your Hevy workouts</span>
            </span>
            <ChevronRight className="h-5 w-5 text-muted-foreground" aria-hidden="true" />
          </Link>
        </nav>
      </main>
    </div>
  );
}
