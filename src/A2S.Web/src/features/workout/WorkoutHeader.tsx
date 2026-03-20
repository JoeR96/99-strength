import { Link } from "react-router-dom";
import { Button } from "@/components/ui/button";

interface WorkoutHeaderProps {
  dayName: string;
  dayNumber: number;
  currentWeek: number;
  workoutName: string;
  isPrefilled: boolean;
  completedSetsCount: number;
  totalSetsCount: number;
  completedExercisesCount: number;
  totalExercisesCount: number;
  progressPercentage: number;
}

export function WorkoutHeader({ dayName, dayNumber, currentWeek, workoutName, isPrefilled, completedSetsCount, totalSetsCount, completedExercisesCount, totalExercisesCount, progressPercentage }: WorkoutHeaderProps) {
  return (
    <>
      {/* Sticky Progress Bar */}
      <div className="sticky top-0 z-40 bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60 border-b">
        <div className="max-w-4xl mx-auto px-4 py-3">
          <div className="flex items-center justify-between mb-2">
            <div className="flex items-center gap-2 text-sm font-medium">
              <span>Day {dayNumber}</span>
              <span className="text-muted-foreground">-</span>
              <span>Week {currentWeek}</span>
              <span className="text-muted-foreground ml-4">
                {completedExercisesCount}/{totalExercisesCount} exercises done
              </span>
            </div>
            <span className="text-sm text-muted-foreground">{Math.round(progressPercentage)}%</span>
          </div>
          <div className="h-2 bg-muted rounded-full overflow-hidden" role="progressbar" aria-valuenow={Math.round(progressPercentage)} aria-valuemin={0} aria-valuemax={100} aria-label="Workout progress">
            <div className="h-full bg-primary transition-all duration-300 ease-out" style={{ width: `${progressPercentage}%` }} />
          </div>
        </div>
      </div>

      {/* Session Info */}
      <div className="mb-6">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-bold" data-testid="session-title">
              {dayName} - Week {currentWeek}
            </h1>
            <p className="text-muted-foreground">
              {workoutName} - Block {Math.ceil(currentWeek / 7)}
            </p>
          </div>
          <Link to="/workout"><Button variant="outline">Cancel</Button></Link>
        </div>

        {isPrefilled && (
          <div className="mt-3 p-3 bg-blue-50 dark:bg-blue-950/30 border border-blue-200 dark:border-blue-800 rounded-lg">
            <div className="flex items-center gap-2 text-blue-700 dark:text-blue-300">
              <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
              <span className="font-medium">Data pulled from Hevy</span>
            </div>
            <p className="text-sm text-blue-600 dark:text-blue-400 mt-1">
              Sets are pre-filled and marked as done. Click a set to edit if needed, then complete the workout.
            </p>
          </div>
        )}

        <div className="mt-4">
          <div className="flex justify-between text-sm text-muted-foreground mb-1">
            <span>Progress</span>
            <span data-testid="sets-progress">{completedSetsCount} / {totalSetsCount} sets</span>
          </div>
          <div className="h-2 bg-muted rounded-full overflow-hidden" role="progressbar" aria-valuenow={Math.round((completedSetsCount / totalSetsCount) * 100)} aria-valuemin={0} aria-valuemax={100} aria-label="Sets completion progress">
            <div className="h-full bg-primary transition-all duration-300" style={{ width: `${(completedSetsCount / totalSetsCount) * 100}%` }} />
          </div>
        </div>

        {completedSetsCount > 0 && (
          <div className="mt-2 flex items-center gap-1.5 text-xs text-muted-foreground">
            <svg className="w-3.5 h-3.5 text-green-500" fill="currentColor" viewBox="0 0 20 20">
              <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
            </svg>
            <span>Progress saved automatically</span>
          </div>
        )}
      </div>
    </>
  );
}
