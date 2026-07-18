import { useState } from "react";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { useWorkoutHistory } from "@/hooks/useWorkouts";
import { EditExerciseConfigModal, type ExerciseConfigUpdate } from "./EditExerciseConfigModal";
import {
  getProgressionLabel,
  groupByProgressionType,
  CurrentStateSummary,
  SegmentTable,
} from "./ExerciseProgressionTables";
import type {
  ExerciseDto,
  ProgressionConfigRequest,
} from "@/types/workout";

interface ExerciseProgressionModalProps {
  exercise: ExerciseDto;
  workoutId: string;
  blockSequence: number[];
  isOpen: boolean;
  onClose: () => void;
  onSave?: (exerciseId: string, config: ExerciseConfigUpdate) => Promise<void>;
  onChangeProgression?: (exerciseId: string, config: ProgressionConfigRequest) => Promise<void>;
  onWorkoutUpdated?: () => void;
}

export function ExerciseProgressionModal({
  exercise,
  workoutId,
  isOpen,
  onClose,
  onSave,
  onChangeProgression,
  onWorkoutUpdated,
}: ExerciseProgressionModalProps) {
  const { data: history, isLoading } = useWorkoutHistory(
    workoutId,
    isOpen
  );
  const [showEditConfig, setShowEditConfig] = useState(false);

  if (!isOpen) return null;

  const exerciseHistory = history?.exerciseHistories?.find(
    (h) => h.exerciseId === exercise.id
  );
  const weeklyHistory = exerciseHistory?.weeklyHistory ?? [];
  const progressionChanges = exerciseHistory?.progressionChanges ?? [];

  const progressionLabel = getProgressionLabel(exercise.progression.type);

  // Group weekly history by progression type segments
  const segments = groupByProgressionType(
    weeklyHistory,
    progressionChanges,
    exercise.progression.type
  );
  const hasMultipleSegments = segments.length > 1;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div
        className="absolute inset-0 bg-black/50 backdrop-blur-sm"
        onClick={onClose}
      />
      <Card className="relative w-full max-w-2xl max-h-[85vh] mx-4 flex flex-col">
        {/* Header */}
        <div className="p-4 border-b">
          <div className="flex items-center justify-between mb-2">
            <h2 className="text-lg font-bold">{exercise.name}</h2>
            <div className="flex items-center gap-2">
              <span className="text-xs px-2 py-0.5 rounded-full bg-primary/10 text-primary">
                {progressionLabel}
              </span>
              <span className="text-xs px-2 py-0.5 rounded-full bg-muted text-muted-foreground">
                Day {exercise.assignedDay}
              </span>
            </div>
          </div>
          <CurrentStateSummary exercise={exercise} />
        </div>

        {/* Body */}
        <div className="flex-1 overflow-y-auto p-4">
          {isLoading ? (
            <div className="text-center py-8">
              <div className="inline-block animate-spin rounded-full h-6 w-6 border-b-2 border-primary" />
              <p className="mt-2 text-sm text-muted-foreground">
                Loading history...
              </p>
            </div>
          ) : weeklyHistory.length === 0 ? (
            <div className="text-center py-8">
              <p className="text-muted-foreground">
                No completed weeks yet. Complete your first session to see
                progression data here.
              </p>
            </div>
          ) : (
            <>
              {segments.map((segment, idx) => (
                <div key={`${segment.progressionType}-${segment.startWeek}`}>
                  {/* Divider for progression type change (not for first segment) */}
                  {idx > 0 && (
                    <div className="my-5 flex items-center gap-3">
                      <div className="flex-1 border-t-2 border-primary/30" />
                      <div className="flex items-center gap-2 text-xs px-3 py-1.5 rounded-full bg-primary/10">
                        <svg className="w-3.5 h-3.5 text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" />
                        </svg>
                        <span className="text-primary font-medium">
                          Changed to {getProgressionLabel(segment.progressionType)}
                        </span>
                        {segment.changeInfo && (
                          <span className="text-muted-foreground">
                            · W{segment.changeInfo.weekNumber} · {new Date(segment.changeInfo.occurredAt).toLocaleDateString()}
                          </span>
                        )}
                      </div>
                      <div className="flex-1 border-t-2 border-primary/30" />
                    </div>
                  )}

                  {/* Section header */}
                  <h3 className="text-sm font-semibold text-muted-foreground mb-3">
                    {hasMultipleSegments ? (
                      <>
                        {getProgressionLabel(segment.progressionType)}
                        {" — "}
                        W{segment.startWeek}
                        {segment.startWeek !== segment.endWeek && `-W${segment.endWeek}`}
                        {" "}({segment.weeks.length} week{segment.weeks.length !== 1 ? "s" : ""})
                      </>
                    ) : (
                      <>Week-by-Week Progression ({weeklyHistory.length} weeks)</>
                    )}
                  </h3>

                  {/* Render the appropriate table */}
                  <SegmentTable
                    segment={segment}
                    exercise={exercise}
                    exerciseTrainingMax={exerciseHistory?.trainingMax ?? undefined}
                  />
                </div>
              ))}
            </>
          )}
        </div>

        {/* Footer */}
        <div className="p-4 border-t flex flex-col-reverse gap-2 sm:flex-row sm:justify-between sm:items-center">
          <div>
            {onChangeProgression && (
              <Button
                variant="outline"
                size="sm"
                onClick={() => setShowEditConfig(true)}
                className="text-sm w-full sm:w-auto"
              >
                <svg className="w-4 h-4 mr-1.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" />
                </svg>
                Change Progression Type
              </Button>
            )}
          </div>
          <Button variant="outline" onClick={onClose} className="w-full sm:w-auto">
            Close
          </Button>
        </div>
      </Card>

      {/* Edit Exercise Config Modal (stacked above) */}
      {showEditConfig && onSave && onChangeProgression && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center">
          <div className="absolute inset-0 bg-black/30" onClick={() => setShowEditConfig(false)} />
          <div className="relative">
            <EditExerciseConfigModal
              exercise={exercise}
              isOpen={showEditConfig}
              onClose={() => setShowEditConfig(false)}
              onSave={async (exerciseId, config) => {
                await onSave(exerciseId, config);
                setShowEditConfig(false);
                onWorkoutUpdated?.();
              }}
              onChangeProgression={async (exerciseId, config) => {
                await onChangeProgression(exerciseId, config);
                setShowEditConfig(false);
                onWorkoutUpdated?.();
              }}
            />
          </div>
        </div>
      )}
    </div>
  );
}
