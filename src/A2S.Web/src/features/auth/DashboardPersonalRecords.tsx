/**
 * DashboardPersonalRecords — best AMRAP set per main lift, from the workout history
 * the dashboard already loads (shares the `useWorkoutHistory` cache with
 * DashboardExerciseTracking, so no extra request).
 */

import { useMemo } from 'react';
import { useWorkoutHistory } from '@/hooks/useWorkouts';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import type { WorkoutDto } from '@/types/workout';
import { findPersonalRecords } from '@/lib/personalRecords';

export function DashboardPersonalRecords({ workout }: { workout?: WorkoutDto | null }) {
  const { data: history } = useWorkoutHistory(workout?.id, !!workout);
  const records = useMemo(
    () => findPersonalRecords(history?.exerciseHistories ?? []).slice(0, 4),
    [history]
  );

  return (
    <Card className="md:col-span-2 lg:col-span-3 overflow-hidden">
      <CardHeader className="pb-4">
        <CardTitle className="flex items-center gap-2">
          <svg className="h-5 w-5 text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor" aria-hidden="true">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 3v4M3 5h4M6 17v4m-2-2h4m5-16l2.286 6.857L21 12l-5.714 2.143L13 21l-2.286-6.857L5 12l5.714-2.143L13 3z" />
          </svg>
          Personal Records
        </CardTitle>
        <CardDescription>Best AMRAP set on each main lift, ranked by estimated 1RM</CardDescription>
      </CardHeader>
      <CardContent>
        {records.length === 0 ? (
          <div className="flex flex-col items-center justify-center rounded-lg border-2 border-dashed border-border/50 bg-muted/10 py-12">
            <p className="text-sm font-medium text-muted-foreground">No personal records yet</p>
            <p className="text-xs text-muted-foreground mt-1">Complete workouts to track your PRs</p>
          </div>
        ) : (
          <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {records.map((pr) => (
              <li key={pr.exerciseId} className="rounded-lg bg-muted/30 p-4">
                <p className="text-sm font-semibold text-foreground">{pr.name}</p>
                <p className="mt-2 text-2xl font-semibold text-primary">
                  {pr.weight} {pr.unit} × {pr.reps}
                </p>
                <p className="text-caption mt-1">
                  e1RM {Math.round(pr.estimatedOneRepMax)} {pr.unit} · Week {pr.weekNumber}
                </p>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}
