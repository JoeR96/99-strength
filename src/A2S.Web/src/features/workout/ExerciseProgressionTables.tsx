import type {
  ExerciseDto,
  LinearProgressionDto,
  RepsPerSetProgressionDto,
  MinimalSetsProgressionDto,
  WeeklyPerformanceDto,
  ProgressionChangeDto,
} from "@/types/workout";
import { WeightUnit as WU } from "@/types/workout";

export function formatWeight(value: number, unit?: string): string {
  return `${value}${unit?.toLowerCase() === "pounds" ? "lbs" : "kg"}`;
}

/** Format TM with enough decimals to show changes (trim trailing zeros) */
export function formatTm(value: number, unit: string): string {
  return `${Number(value.toFixed(2))}${unit}`;
}

export function getOutcome(
  reps: number[],
  repRange: { minimum: number; maximum: number },
): { label: string; color: string } {
  if (reps.every((r) => r >= repRange.maximum)) return { label: "SUCCESS", color: "text-green-500" };
  if (reps.some((r) => r < repRange.minimum)) return { label: "FAILED", color: "text-red-500" };
  return { label: "MAINTAINED", color: "text-yellow-500" };
}

export function getProgressionLabel(type: string): string {
  const labels: Record<string, string> = { Linear: "Linear (A2S)", RepsPerSet: "Reps Per Set", MinimalSets: "Minimal Sets" };
  return labels[type] ?? type;
}

/** Get the TM for a given week from snapshot data. */
export function getLinearTmForWeek(
  week: WeeklyPerformanceDto,
  _currentTm: number | undefined,
): number | null {
  if (week.trainingMaxAtWeek != null) return week.trainingMaxAtWeek;
  return null;
}

export interface ProgressionSegment {
  progressionType: string;
  weeks: WeeklyPerformanceDto[];
  startWeek: number;
  endWeek: number;
  changeInfo?: ProgressionChangeDto;
}

export function groupByProgressionType(
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
      segments.push({
        progressionType: currentType,
        weeks: currentWeeks,
        startWeek: currentWeeks[0].weekNumber,
        endWeek: currentWeeks[currentWeeks.length - 1].weekNumber,
      });

      currentType = weekType;
      currentWeeks = [week];
    } else {
      currentWeeks.push(week);
    }
  }

  segments.push({
    progressionType: currentType,
    weeks: currentWeeks,
    startWeek: currentWeeks[0].weekNumber,
    endWeek: currentWeeks[currentWeeks.length - 1].weekNumber,
  });

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

export function LinearTable({
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

export function RepsPerSetTable({
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

export function MinimalSetsTable({
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

export function CurrentStateSummary({ exercise }: { exercise: ExerciseDto }) {
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
            {rp.repRange?.minimum}-{rp.repRange?.maximum}
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

export function SegmentTable({
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
