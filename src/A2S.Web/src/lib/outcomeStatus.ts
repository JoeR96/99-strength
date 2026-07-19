export type OutcomeStatus = "success" | "failed" | "deload" | "maintained";

/**
 * Classify a free-text progression change string into a status.
 * Extracted from CompletionSummary/SimulationPage to remove duplicated,
 * order-dependent `.includes()` chains and off-token colours.
 */
export function outcomeToStatus(change: string): OutcomeStatus {
  const c = change.toLowerCase();
  if (c.includes("increased") || c.includes("added")) return "success";
  if (c.includes("decreased") || c.includes("reduced")) return "failed";
  if (c.includes("deload")) return "deload";
  return "maintained";
}

const STATUS_LABEL: Record<OutcomeStatus, string> = {
  success: "SUCCESS",
  failed: "FAILED",
  deload: "DELOAD",
  maintained: "MAINTAINED",
};

export function outcomeLabel(status: OutcomeStatus): string {
  return STATUS_LABEL[status];
}

/** Token-based badge classes for an outcome status (fill + foreground). */
export function statusBadgeClass(status: OutcomeStatus): string {
  switch (status) {
    case "success":
      return "text-success bg-success/15";
    case "failed":
      return "text-destructive bg-destructive/15";
    case "deload":
      return "text-primary bg-primary/10";
    case "maintained":
      return "text-warning bg-warning/15";
  }
}

/** Token foreground-only class for a raw simulation outcome value. */
export function simOutcomeClass(outcome: string): string {
  switch (outcome) {
    case "Success":
      return "text-success";
    case "Fail":
      return "text-destructive";
    default:
      return "text-warning";
  }
}
