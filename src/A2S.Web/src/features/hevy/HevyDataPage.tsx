/**
 * Hevy Data Page
 * Browse Hevy workouts and view per-exercise historical data with charts
 */

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Navbar } from '@/components/layout/Navbar';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { useHevy } from '@/contexts/HevyContext';
import { apiClient } from '@/api/apiClient';
import { HevyExerciseModal } from './HevyExerciseModal';
import { HevySettings } from '@/components/hevy/HevySettings';

interface HevyExerciseSummary {
  exerciseTemplateId: string;
  title: string;
  setCount: number;
  bestWeight: number;
  bestReps: number;
  totalVolume: number;
}

interface HevyWorkoutSummary {
  id: string;
  title: string;
  startTime: string;
  endTime: string;
  exerciseCount: number;
  exercises: HevyExerciseSummary[];
}

interface HevyWorkoutListResponse {
  workouts: HevyWorkoutSummary[];
  page: number;
  pageCount: number;
}

function useHevyWorkouts(page: number, apiKey: string | null) {
  return useQuery({
    queryKey: ['hevy-data-workouts', page],
    queryFn: async () => {
      const response = await apiClient.get<HevyWorkoutListResponse>(
        `/hevy/data/workouts?page=${page}&pageSize=10`,
        { headers: { 'X-Hevy-Api-Key': apiKey ?? '' } }
      );
      return response.data;
    },
    enabled: !!apiKey,
    staleTime: 1000 * 60 * 5,
  });
}

export function HevyDataPage() {
  const { apiKey, isConfigured, isValid } = useHevy();
  const [page, setPage] = useState(1);
  const [selectedExercise, setSelectedExercise] = useState<{
    templateId: string;
    title: string;
  } | null>(null);

  const { data, isLoading, error } = useHevyWorkouts(page, apiKey);

  const formatDate = (dateStr: string) => {
    try {
      return new Date(dateStr).toLocaleDateString(undefined, {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
      });
    } catch {
      return dateStr;
    }
  };

  const formatDuration = (start: string, end: string) => {
    try {
      const ms = new Date(end).getTime() - new Date(start).getTime();
      const mins = Math.round(ms / 60000);
      if (mins >= 60) {
        const hrs = Math.floor(mins / 60);
        const remaining = mins % 60;
        return `${hrs}h ${remaining}m`;
      }
      return `${mins}m`;
    } catch {
      return '';
    }
  };

  return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <div className="container mx-auto px-4 py-6 max-w-4xl">
        <div className="mb-6">
          <h1 className="text-2xl font-bold">Hevy Data</h1>
          <p className="text-muted-foreground">
            Browse your Hevy workouts and explore exercise history
          </p>
        </div>

        {!isConfigured || isValid === false ? (
          <div className="space-y-4">
            <Card>
              <CardContent className="py-8">
                <div className="text-center text-muted-foreground">
                  <svg className="h-12 w-12 mx-auto mb-4 opacity-50" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M13.828 10.172a4 4 0 00-5.656 0l-4 4a4 4 0 105.656 5.656l1.102-1.101m-.758-4.899a4 4 0 005.656 0l4-4a4 4 0 00-5.656-5.656l-1.1 1.1" />
                  </svg>
                  <p className="mb-2">Connect your Hevy account to view workout data</p>
                  <p className="text-sm">Enter your API key below to get started</p>
                </div>
              </CardContent>
            </Card>
            <HevySettings />
          </div>
        ) : (
          <div className="space-y-4">
            {isLoading && (
              <Card>
                <CardContent className="pt-6">
                  <div className="flex items-center justify-center py-8">
                    <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
                    <span className="ml-3 text-muted-foreground">Loading workouts...</span>
                  </div>
                </CardContent>
              </Card>
            )}

            {error && (
              <Card>
                <CardContent className="pt-6">
                  <p className="text-destructive">
                    Failed to load workouts: {error instanceof Error ? error.message : 'Unknown error'}
                  </p>
                </CardContent>
              </Card>
            )}

            {data?.workouts.map((workout) => (
              <Card key={workout.id}>
                <CardHeader className="pb-3">
                  <div className="flex items-center justify-between">
                    <CardTitle className="text-lg">{workout.title}</CardTitle>
                    <div className="flex items-center gap-3 text-sm text-muted-foreground">
                      <span>{formatDuration(workout.startTime, workout.endTime)}</span>
                      <span>{formatDate(workout.startTime)}</span>
                    </div>
                  </div>
                </CardHeader>
                <CardContent>
                  <div className="grid gap-2">
                    {workout.exercises.map((exercise, idx) => (
                      <button
                        key={`${exercise.exerciseTemplateId}-${idx}`}
                        onClick={() =>
                          setSelectedExercise({
                            templateId: exercise.exerciseTemplateId,
                            title: exercise.title,
                          })
                        }
                        className="flex items-center justify-between rounded-lg border border-border p-3 text-left transition-colors hover:bg-muted/50"
                      >
                        <div>
                          <p className="font-medium text-foreground">{exercise.title}</p>
                          <p className="text-sm text-muted-foreground">
                            {exercise.setCount} sets · Best: {exercise.bestWeight}kg × {exercise.bestReps}
                          </p>
                        </div>
                        <div className="text-right">
                          <p className="text-sm font-medium text-foreground">
                            {Math.round(exercise.totalVolume).toLocaleString()}kg
                          </p>
                          <p className="text-xs text-muted-foreground">volume</p>
                        </div>
                      </button>
                    ))}
                    {workout.exercises.length === 0 && (
                      <p className="py-2 text-sm text-muted-foreground">No exercises recorded</p>
                    )}
                  </div>
                </CardContent>
              </Card>
            ))}

            {data && data.workouts.length === 0 && !isLoading && (
              <Card>
                <CardContent className="pt-6">
                  <p className="text-center text-muted-foreground py-4">
                    No workouts found. Start logging workouts in Hevy!
                  </p>
                </CardContent>
              </Card>
            )}

            {/* Pagination */}
            {data && data.pageCount > 1 && (
              <div className="flex items-center justify-center gap-2 pt-2">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  disabled={page <= 1}
                >
                  Previous
                </Button>
                <span className="text-sm text-muted-foreground">
                  Page {page} of {data.pageCount}
                </span>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setPage((p) => Math.min(data.pageCount, p + 1))}
                  disabled={page >= data.pageCount}
                >
                  Next
                </Button>
              </div>
            )}
          </div>
        )}

        {/* Exercise History Modal */}
        {selectedExercise && (
          <HevyExerciseModal
            exerciseTemplateId={selectedExercise.templateId}
            exerciseTitle={selectedExercise.title}
            apiKey={apiKey}
            onClose={() => setSelectedExercise(null)}
          />
        )}
      </div>
    </div>
  );
}
