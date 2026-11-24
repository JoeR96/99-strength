import type { CardioStageProgressionDto } from "@/types/workout";

/** A cardio exercise's stage and today's prescription (or what to set first), shared by the plan views. */
export function CardioStageSummary({ progression, size = "xs" }: { progression: CardioStageProgressionDto; size?: "xs" | "sm" }) {
  return (
    <div className={`${size === "sm" ? "text-sm" : "text-xs"} text-muted-foreground mt-1 space-y-0.5`} data-testid="cardio-stage">
      <div className="flex justify-between">
        <span>Stage:</span>
        <span className="font-medium text-foreground">{progression.stageNumber} · {progression.stageName}</span>
      </div>
      <div className="flex justify-between gap-3">
        <span>Today:</span>
        <span className="font-medium text-foreground text-right">
          {progression.isSettingsPending ? "Set in gym mode first" : progression.prescription}
        </span>
      </div>
    </div>
  );
}
