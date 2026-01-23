import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Navbar } from '@/components/layout/Navbar';
import { useAllWorkouts, useSetActiveWorkout, useDeleteWorkout } from '@/hooks/useWorkouts';
import type { WorkoutSummaryDto } from '@/types/workout';
import toast from 'react-hot-toast';

export function ProgramsPage() {
  const navigate = useNavigate();
  const { data: workouts, isLoading, error } = useAllWorkouts();
  const setActiveMutation = useSetActiveWorkout();
  const deleteMutation = useDeleteWorkout();
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const handleSetActive = async (workoutId: string) => {
    try {
      await setActiveMutation.mutateAsync(workoutId);
      toast.success('Program set as active');
    } catch {
      toast.error('Failed to set program as active');
    }
  };

  const handleDelete = async (workoutId: string) => {
    if (!confirm('Are you sure you want to delete this program? This action cannot be undone.')) {
      return;
    }

    setDeletingId(workoutId);
    try {
      await deleteMutation.mutateAsync(workoutId);
      toast.success('Program deleted');
    } catch {
      toast.error('Failed to delete program');
    } finally {
      setDeletingId(null);
    }
  };

  if (isLoading) {
    return (
      <div className="min-h-screen bg-background">
        <Navbar />
        <div className="max-w-6xl mx-auto p-6">
          <div className="text-center py-12">
            <div className="inline-block animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
            <p className="mt-4 text-muted-foreground">Loading programs...</p>
          </div>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="min-h-screen bg-background">
        <Navbar />
        <div className="max-w-6xl mx-auto p-6">
          <Card className="p-8 text-center">
            <p className="text-destructive">Failed to load programs</p>
            <Button className="mt-4" onClick={() => window.location.reload()}>
              Retry
            </Button>
          </Card>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <div className="max-w-6xl mx-auto p-6">
      {/* Header */}
      <div className="flex items-center justify-between mb-8">
        <div>
          <h1 className="text-3xl font-bold">My Programs</h1>
          <p className="text-muted-foreground mt-1">
            Manage your workout programs. Only one can be active at a time.
          </p>
        </div>
        <Button onClick={() => navigate('/setup')} variant="glow">
          Create New Program
        </Button>
      </div>

      {/* Programs List */}
      {!workouts || workouts.length === 0 ? (
        <Card className="p-8 text-center">
          <div className="flex flex-col items-center justify-center">
            <svg className="h-16 w-16 text-muted-foreground/30 mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
            </svg>
            <h2 className="text-xl font-semibold mb-2">No Programs Yet</h2>
            <p className="text-muted-foreground mb-6">
              Create your first A2S workout program to get started.
            </p>
            <Button onClick={() => navigate('/setup')} variant="glow">
              Create Your First Program
            </Button>
          </div>
        </Card>
      ) : (
        <div className="grid gap-4">
          {workouts.map((workout) => (
            <ProgramCard
              key={workout.id}
              workout={workout}
              onSetActive={handleSetActive}
              onDelete={handleDelete}
              isSettingActive={setActiveMutation.isPending}
              isDeleting={deletingId === workout.id}
            />
          ))}
        </div>
      )}
      </div>
    </div>
  );
}

interface ProgramCardProps {
  workout: WorkoutSummaryDto;
  onSetActive: (id: string) => void;
  onDelete: (id: string) => void;
  isSettingActive: boolean;
  isDeleting: boolean;
}

function ProgramCard({ workout, onSetActive, onDelete, isSettingActive, isDeleting }: ProgramCardProps) {
  const navigate = useNavigate();
  const progressPercent = Math.round((workout.currentWeek / workout.totalWeeks) * 100);
  const currentBlock = Math.ceil(workout.currentWeek / 7);

  const statusColors: Record<string, string> = {
    Active: 'bg-green-500/10 text-green-500 border-green-500/30',
    NotStarted: 'bg-yellow-500/10 text-yellow-500 border-yellow-500/30',
    Paused: 'bg-orange-500/10 text-orange-500 border-orange-500/30',
    Completed: 'bg-blue-500/10 text-blue-500 border-blue-500/30',
  };

  return (
    <Card className={`overflow-hidden transition-all ${workout.isActive ? 'ring-2 ring-primary' : ''}`}>
      <div className="flex flex-col md:flex-row">
        {/* Main Content */}
        <div className="flex-1 p-6">
          <div className="flex items-start justify-between mb-4">
            <div>
              <div className="flex items-center gap-3 mb-2">
                <h3 className="text-xl font-bold">{workout.name}</h3>
                <span className={`px-2 py-0.5 text-xs font-medium rounded-full border ${statusColors[workout.status] || statusColors.NotStarted}`}>
                  {workout.status}
                </span>
                {workout.isActive && (
                  <span className="px-2 py-0.5 text-xs font-medium rounded-full bg-primary/10 text-primary border border-primary/30">
                    Active Program
                  </span>
                )}
              </div>
              <p className="text-sm text-muted-foreground">
                {workout.variant}-Day Split | {workout.exerciseCount} exercises | {workout.totalWeeks} weeks
              </p>
            </div>
          </div>

          {/* Progress */}
          <div className="mb-4">
            <div className="flex items-center justify-between text-sm mb-1">
              <span className="text-muted-foreground">
                Week {workout.currentWeek} of {workout.totalWeeks} (Block {currentBlock})
              </span>
              <span className="font-medium">{progressPercent}%</span>
            </div>
            <div className="h-2 bg-muted rounded-full overflow-hidden">
              <div
                className="h-full bg-primary transition-all duration-300"
                style={{ width: `${progressPercent}%` }}
              />
            </div>
          </div>

          {/* Dates */}
          <div className="flex gap-6 text-sm text-muted-foreground">
            <div>
              <span className="font-medium">Created:</span>{' '}
              {new Date(workout.createdAt).toLocaleDateString()}
            </div>
            {workout.startedAt && (
              <div>
                <span className="font-medium">Started:</span>{' '}
                {new Date(workout.startedAt).toLocaleDateString()}
              </div>
            )}
            {workout.completedAt && (
              <div>
                <span className="font-medium">Completed:</span>{' '}
                {new Date(workout.completedAt).toLocaleDateString()}
              </div>
            )}
          </div>
        </div>

        {/* Actions */}
        <div className="flex flex-row md:flex-col gap-2 p-4 md:p-6 border-t md:border-t-0 md:border-l border-border bg-muted/20">
          {workout.isActive ? (
            <Button
              variant="default"
              size="sm"
              className="flex-1 md:flex-none"
              onClick={() => navigate('/workout')}
            >
              View Workout
            </Button>
          ) : (
            <Button
              variant="outline"
              size="sm"
              className="flex-1 md:flex-none"
              onClick={() => onSetActive(workout.id)}
              disabled={isSettingActive || workout.status === 'Completed'}
            >
              {isSettingActive ? 'Setting...' : 'Set Active'}
            </Button>
          )}
          <Button
            variant="ghost"
            size="sm"
            className="flex-1 md:flex-none text-destructive hover:text-destructive hover:bg-destructive/10"
            onClick={() => onDelete(workout.id)}
            disabled={isDeleting}
          >
            {isDeleting ? 'Deleting...' : 'Delete'}
          </Button>
        </div>
      </div>
    </Card>
  );
}
