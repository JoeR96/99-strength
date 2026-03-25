import { useState } from "react";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { useWorkoutHistory } from "@/hooks/useWorkouts";
import { EditExerciseConfigModal, type ExerciseConfigUpdate } from "./EditExerciseConfigModal";
import type {
  ExerciseDto,
  LinearProgressionDto,
  RepsPerSetProgressionDto,
  MinimalSetsProgressionDto,
  WeeklyPerformanceDto,
  ProgressionChangeDto,
  ProgressionConfigRequest,
} from "@/types/workout";
import { WeightUnit as WU } from "@/types/workout";

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

function formatWeight(value: number, unit?: string): string {
  const u = unit?.toLowerCase() === "pounds" ? "lbs" : "kg";
  return `${value}${u}`;
}

/** Format TM with enough decimals to show changes (trim trailing zeros) */
function formatTm(value: number, unit: string): string {
  // Show up to 2 decimal places, but trim unnecessary trailing zeros
  const formatted = Number(value.toFixed(2));
  return `${formatted}${unit}`;
}

function getOutcome(
  reps: number[],
  repRange: { minimum: number; maximum: number }
): { label: string; color: string } {
  const allHitMax = reps.every((r) => r >= repRange.maximum);
  const anyBelowMin = reps.some((r) => r < repRange.minimum);
  if (allHitMax) return { label: "SUCCESS", color: "text-green-500" };
  if (anyBelowMin) return { label: "FAILED", color: "text-red-500" };
  return { label: "MAINTAINED", color: "text-yellow-500" };
}

function getProgressionLabel(type: string): string {
  if (type === "Linear") return "Linear (A2S)";
  if (type === "RepsPerSet") return "Reps Per Set";
  if (type === "MinimalSets") return "Minimal Sets";
  return type;
}

/**
 * For Linear exercises, get the TM for a given week.
 * Uses snapshot data if available, otherwise falls back to the current TM
 * from ExerciseHistoryDto (which represents the LATEST value).
 */
function getLinearTmForWeek(
  week: WeeklyPerformanceDto,
  _currentTm: number | undefined
): number | null {
  if (week.trainingMaxAtWeek != null) return week.trainingMaxAtWeek;
  return null;
}

// --- Progression Segment grouping ---

interface ProgressionSegment {
  progressionType: string;
  weeks: WeeklyPerformanceDto[];
  startWeek: number;
  endWeek: number;
  changeInfo?: ProgressionChangeDto;
}

function groupByProgressionType(
  weeklyHistory: WeeklyPerformanceDto[],
  progressionChanges: ProgressionChangeDto[],
  currentProgressionType: string
): ProgressionSegment[] {
  if (weeklyHistory.length === 0) return [];

  const segments: ProgressionSegment[] = [];
  let currentType = weeklyHistory[0].progressionTypeAtWeek || currentProgressionType;
  let currentWeeks: WeeklyPerformanceDto[] = [weeklyHistory[0]];

  for (let i = 1; i < weeklyHistory.length; i++) {
    const week = weeklyHistory[i];
    const weekType = week.progressionTypeAtWeek || currentProgressionType;

    if (weekType !== currentType) {
      // End current segment
      segments.push({
        progressionType: currentType,
        weeks: currentWeeks,
        startWeek: currentWeeks[0].weekNumber,
        endWeek: currentWeeks[currentWeeks.length - 1].weekNumber,
      });

      // Find the audit entry for this transition
      const changeEntry = progressionChanges.find(
        (c) => c.newProgressionType === weekType
          && c.weekNumber <= week.weekNumber
          && (!segments.length || c.weekNumber > segments[segments.length - 1].startWeek)
      );

      currentType = weekType;
      currentWeeks = [week];

      // The new segment will get the change info attached below
      if (changeEntry) {
        // We'll store it temporarily to attach after pushing
      }
    } else {
      currentWeeks.push(week);
    }
  }

  // Push last segment
  segments.push({
    progressionType: currentType,
    weeks: currentWeeks,
    startWeek: currentWeeks[0].weekNumber,
    endWeek: currentWeeks[currentWeeks.length - 1].weekNumber,
  });

  // Attach changeInfo from progressionChanges to each segment after the first
  for (let i = 1; i < segments.length; i++) {
    const seg = segments[i];
    const change = progressionChanges.find(
      (c) => c.newProgressionType === seg.progressionType
        && c.weekNumber <= seg.startWeek
    );
    if (change) {
      segments[i] = { ...seg, changeInfo: change };
    }
  }

  return segments;
}

// --- Table Components ---

function LinearTable({
  weeklyHistory,
  exercise,
  currentExerciseTm,
  unitStrOverride,
}: {
  weeklyHistory: WeeklyPerformanceDto[];
  exercise: ExerciseDto;
  currentExerciseTm?: number;
  unitStrOverride?: string;
}) {
  const isCurrentlyLinear = exercise.progression.type === "Linear";
  const linearProg = isCurrentlyLinear ? (exercise.progression as LinearProgressionDto) : null;
  const unitStr = unitStrOverride
    ?? (linearProg ? (linearProg.trainingMax.unit === WU.Kilograms ? "kg" : "lbs") : "kg");

  // Build rows with TM before (snapshot) and TM after (next week's snapshot or current TM)
  const rows = weeklyHistory.map((week, idx) => {
    const tmBefore = getLinearTmForWeek(week, currentExerciseTm);
    const nextWeek = idx < weeklyHistory.length - 1 ? weeklyHistory[idx + 1] : null;
    const tmAfter = nextWeek
      ? getLinearTmForWeek(nextWeek, currentExerciseTm)
      : currentExerciseTm ?? null;

    const tmDelta =
      tmBefore != null && tmAfter != null ? tmAfter - tmBefore : null;

    const setsReps = week.sets.map((s) => s.actualReps).join(", ");
    const amrapSet = week.sets.find((s) => s.wasAmrap);

    return { week, tmBefore, tmAfter, tmDelta, setsReps, amrapSet };
  });

  const hasSnapshotData = rows.some((r) => r.tmBefore != null);

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-left text-muted-foreground">
            <th className="py-2 px-2 font-medium">Week</th>
            <th className="py-2 px-2 font-medium">Block</th>
            {hasSnapshotData && (
              <th className="py-2 px-2 font-medium">TM Before</th>
            )}
            <th className="py-2 px-2 font-medium">Sets x Reps</th>
            <th className="py-2 px-2 font-medium">AMRAP</th>
            {hasSnapshotData && (
              <>
                <th className="py-2 px-2 font-medium">TM After</th>
                <th className="py-2 px-2 font-medium">Change</th>
              </>
            )}
          </tr>
        </thead>
        <tbody>
          {rows.map(({ week, tmBefore, tmAfter, tmDelta, setsReps, amrapSet }) => (
            <tr
              key={week.weekNumber}
              className={`border-b ${
                week.isDeloadWeek
                  ? "bg-muted/30 italic text-muted-foreground"
                  : ""
              }`}
            >
              <td className="py-2 px-2">W{week.weekNumber}</td>
              <td className="py-2 px-2">B{week.blockNumber}</td>
              {hasSnapshotData && (
                <td className="py-2 px-2 font-medium">
                  {tmBefore != null ? formatTm(tmBefore, unitStr) : "-"}
                </td>
              )}
              <td className="py-2 px-2">
                {week.setsCompleted}x[{setsReps}]
              </td>
              <td className="py-2 px-2">
                {amrapSet ? (
                  <span className="font-medium text-primary">
                    {amrapSet.actualReps}
                  </span>
                ) : (
                  <span className="text-muted-foreground">-</span>
                )}
              </td>
              {hasSnapshotData && (
                <>
                  <td className="py-2 px-2 font-medium">
                    {tmAfter != null ? formatTm(tmAfter, unitStr) : "-"}
                  </td>
                  <td className="py-2 px-2">
                    {tmDelta != null ? (
                      <span
                        className={
                          tmDelta > 0
                            ? "text-green-500 font-medium"
                            : tmDelta < 0
                            ? "text-red-500 font-medium"
                            : "text-muted-foreground"
                        }
                      >
                        {tmDelta > 0 ? "+" : ""}
                        {formatTm(tmDelta, unitStr)}
                      </span>
                    ) : (
                      <span className="text-muted-foreground">-</span>
                    )}
                  </td>
                </>
              )}
            </tr>
          ))}
        </tbody>
      </table>
      {!hasSnapshotData && linearProg && (
        <p className="text-xs text-muted-foreground mt-2 italic">
          Training Max history is not available for these weeks (completed before snapshot tracking).
          Current TM: {formatTm(linearProg.trainingMax.value, unitStr)}
        </p>
      )}
    </div>
  );
}

function RepsPerSetTable({
  weeklyHistory,
  exercise,
  repRangeOverride,
  unitStrOverride,
}: {
  weeklyHistory: WeeklyPerformanceDto[];
  exercise: ExerciseDto;
  repRangeOverride?: { minimum: number; maximum: number };
  unitStrOverride?: string;
}) {
  const isCurrentlyRps = exercise.progression.type === "RepsPerSet";
  const rpsProg = isCurrentlyRps ? (exercise.progression as RepsPerSetProgressionDto) : null;
  const repRange = repRangeOverride ?? rpsProg?.repRange;
  const unitStr = unitStrOverride ?? (rpsProg?.weightUnit?.toLowerCase() === "pounds" ? "lbs" : "kg");

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-left text-muted-foreground">
            <th className="py-2 px-2 font-medium">Week</th>
            <th className="py-2 px-2 font-medium">Block</th>
            <th className="py-2 px-2 font-medium">Weight</th>
            <th className="py-2 px-2 font-medium">Sets</th>
            <th className="py-2 px-2 font-medium">Reps Hit</th>
            <th className="py-2 px-2 font-medium">Outcome</th>
            <th className="py-2 px-2 font-medium">Change</th>
          </tr>
        </thead>
        <tbody>
          {weeklyHistory.map((week, idx) => {
            const prevWeek = idx > 0 ? weeklyHistory[idx - 1] : null;
            const weight = week.weightAtWeek;
            const sets = week.setCountAtWeek;
            const prevWeight = prevWeek?.weightAtWeek;
            const prevSets = prevWeek?.setCountAtWeek;

            const repsArr = week.sets.map((s) => s.actualReps);
            const outcome =
              repRange && !week.isDeloadWeek
                ? getOutcome(repsArr, repRange)
                : null;

            const changes: string[] = [];
            if (weight != null && prevWeight != null && weight !== prevWeight) {
              const d = weight - prevWeight;
              changes.push(`${d > 0 ? "+" : ""}${d}${unitStr}`);
            }
            if (sets != null && prevSets != null && sets !== prevSets) {
              const d = sets - prevSets;
              changes.push(`${d > 0 ? "+" : ""}${d} set${Math.abs(d) !== 1 ? "s" : ""}`);
            }

            return (
              <tr
                key={week.weekNumber}
                className={`border-b ${
                  week.isDeloadWeek
                    ? "bg-muted/30 italic text-muted-foreground"
                    : ""
                }`}
              >
                <td className="py-2 px-2">W{week.weekNumber}</td>
                <td className="py-2 px-2">B{week.blockNumber}</td>
                <td className="py-2 px-2 font-medium">
                  {weight != null ? formatWeight(weight, unitStr) : "-"}
                </td>
                <td className="py-2 px-2">{sets ?? week.setsCompleted}</td>
                <td className="py-2 px-2">{repsArr.join(", ")}</td>
                <td className="py-2 px-2">
                  {outcome ? (
                    <span className={`font-medium ${outcome.color}`}>
                      {outcome.label}
                    </span>
                  ) : week.isDeloadWeek ? (
                    <span className="text-muted-foreground">DELOAD</span>
                  ) : (
                    "-"
                  )}
                </td>
                <td className="py-2 px-2">
                  {changes.length > 0 ? (
                    <span className="text-green-500 font-medium">
                      {changes.join(", ")}
                    </span>
                  ) : (
                    <span className="text-muted-foreground">-</span>
                  )}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

function MinimalSetsTable({
  weeklyHistory,
}: {
  weeklyHistory: WeeklyPerformanceDto[];
}) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-left text-muted-foreground">
            <th className="py-2 px-2 font-medium">Week</th>
            <th className="py-2 px-2 font-medium">Block</th>
            <th className="py-2 px-2 font-medium">Weight</th>
            <th className="py-2 px-2 font-medium">Sets</th>
            <th className="py-2 px-2 font-medium">Total Reps</th>
            <th className="py-2 px-2 font-medium">Volume</th>
          </tr>
        </thead>
        <tbody>
          {weeklyHistory.map((week) => (
            <tr
              key={week.weekNumber}
              className={`border-b ${
                week.isDeloadWeek
                  ? "bg-muted/30 italic text-muted-foreground"
                  : ""
              }`}
            >
              <td className="py-2 px-2">W{week.weekNumber}</td>
              <td className="py-2 px-2">B{week.blockNumber}</td>
              <td className="py-2 px-2 font-medium">
                {week.weightAtWeek != null
                  ? formatWeight(week.weightAtWeek)
                  : formatWeight(week.averageWeight)}
              </td>
              <td className="py-2 px-2">{week.setCountAtWeek ?? week.setsCompleted}</td>
              <td className="py-2 px-2">{week.totalReps}</td>
              <td className="py-2 px-2">{Math.round(week.totalVolume)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function CurrentStateSummary({ exercise }: { exercise: ExerciseDto }) {
  const prog = exercise.progression;

  if (prog.type === "Linear") {
    const lp = prog as LinearProgressionDto;
    return (
      <div className="flex flex-wrap gap-4 text-sm">
        <div>
          <span className="text-muted-foreground">TM: </span>
          <span className="font-medium">
            {formatTm(lp.trainingMax.value, lp.trainingMax.unit === WU.Kilograms ? "kg" : "lbs")}
          </span>
        </div>
        <div>
          <span className="text-muted-foreground">Sets: </span>
          <span className="font-medium">{lp.baseSetsPerExercise}</span>
        </div>
        <div>
          <span className="text-muted-foreground">AMRAP: </span>
          <span className={lp.useAmrap ? "text-primary font-medium" : "text-muted-foreground"}>
            {lp.useAmrap ? "Yes" : "No"}
          </span>
        </div>
      </div>
    );
  }

  if (prog.type === "RepsPerSet") {
    const rp = prog as RepsPerSetProgressionDto;
    return (
      <div className="flex flex-wrap gap-4 text-sm">
        <div>
          <span className="text-muted-foreground">Weight: </span>
          <span className="font-medium">
            {rp.currentWeight}
            {rp.weightUnit?.toLowerCase() === "pounds" ? "lbs" : "kg"}
          </span>
        </div>
        <div>
          <span className="text-muted-foreground">Sets: </span>
          <span className="font-medium">
            {rp.currentSetCount}/{rp.targetSets}
          </span>
        </div>
        <div>
          <span className="text-muted-foreground">Range: </span>
          <span className="font-medium">
            {rp.repRange?.minimum}-{rp.repRange?.target}-{rp.repRange?.maximum}
          </span>
        </div>
        {rp.isUnilateral && (
          <div className="text-primary font-medium">Unilateral</div>
        )}
      </div>
    );
  }

  if (prog.type === "MinimalSets") {
    const mp = prog as MinimalSetsProgressionDto;
    return (
      <div className="flex flex-wrap gap-4 text-sm">
        <div>
          <span className="text-muted-foreground">Weight: </span>
          <span className="font-medium">
            {mp.currentWeight}
            {mp.weightUnit?.toLowerCase() === "pounds" ? "lbs" : "kg"}
          </span>
        </div>
        <div>
          <span className="text-muted-foreground">Sets: </span>
          <span className="font-medium">
            {mp.currentSetCount} ({mp.minimumSets}-{mp.maximumSets})
          </span>
        </div>
        <div>
          <span className="text-muted-foreground">Target Reps: </span>
          <span className="font-medium">{mp.targetTotalReps}</span>
        </div>
      </div>
    );
  }

  return null;
}

function SegmentTable({
  segment,
  exercise,
  exerciseTrainingMax,
}: {
  segment: ProgressionSegment;
  exercise: ExerciseDto;
  exerciseTrainingMax?: number;
}) {
  const segType = segment.progressionType;

  if (segType === "Linear") {
    // For historical Linear segments where exercise is now a different type,
    // derive unit from the snapshot data
    const unitOverride = exercise.progression.type !== "Linear"
      ? (segment.weeks[0]?.trainingMaxUnitAtWeek?.toLowerCase() === "pounds" ? "lbs" : "kg")
      : undefined;
    return (
      <LinearTable
        weeklyHistory={segment.weeks}
        exercise={exercise}
        currentExerciseTm={exercise.progression.type === "Linear" ? exerciseTrainingMax : undefined}
        unitStrOverride={unitOverride}
      />
    );
  }

  if (segType === "RepsPerSet") {
    // For historical RepsPerSet segments where exercise is now a different type,
    // fall back to default rep range (8-12) since we can't get it from the snapshot
    const repRangeOverride = exercise.progression.type !== "RepsPerSet"
      ? { minimum: 8, maximum: 12 }
      : undefined;
    const unitOverride = exercise.progression.type !== "RepsPerSet" ? "kg" : undefined;
    return (
      <RepsPerSetTable
        weeklyHistory={segment.weeks}
        exercise={exercise}
        repRangeOverride={repRangeOverride}
        unitStrOverride={unitOverride}
      />
    );
  }

  if (segType === "MinimalSets") {
    return <MinimalSetsTable weeklyHistory={segment.weeks} />;
  }

  return null;
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
        <div className="p-4 border-t flex justify-between items-center">
          <div>
            {onChangeProgression && (
              <Button
                variant="outline"
                size="sm"
                onClick={() => setShowEditConfig(true)}
                className="text-sm"
              >
                <svg className="w-4 h-4 mr-1.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" />
                </svg>
                Change Progression Type
              </Button>
            )}
          </div>
          <Button variant="outline" onClick={onClose}>
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
