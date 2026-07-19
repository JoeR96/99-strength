import { describe, it, expect } from "vitest";
import {
  outcomeToStatus,
  outcomeLabel,
  statusBadgeClass,
  simOutcomeClass,
} from "./outcomeStatus";

describe("outcomeToStatus", () => {
  it("classifies increased/added as success", () => {
    expect(outcomeToStatus("Weight increased to 105kg")).toBe("success");
    expect(outcomeToStatus("Added a set")).toBe("success");
  });
  it("classifies decreased/reduced as failed", () => {
    expect(outcomeToStatus("Weight decreased")).toBe("failed");
    expect(outcomeToStatus("Sets reduced")).toBe("failed");
  });
  it("classifies deload", () => {
    expect(outcomeToStatus("Deload week applied")).toBe("deload");
  });
  it("defaults to maintained", () => {
    expect(outcomeToStatus("Maintained current weight")).toBe("maintained");
    expect(outcomeToStatus("no change")).toBe("maintained");
  });
});

describe("outcomeLabel", () => {
  it("maps status to upper-case label", () => {
    expect(outcomeLabel("success")).toBe("SUCCESS");
    expect(outcomeLabel("failed")).toBe("FAILED");
    expect(outcomeLabel("deload")).toBe("DELOAD");
    expect(outcomeLabel("maintained")).toBe("MAINTAINED");
  });
});

describe("statusBadgeClass", () => {
  it("returns token-only classes (no raw colour literals)", () => {
    for (const s of ["success", "failed", "deload", "maintained"] as const) {
      const cls = statusBadgeClass(s);
      expect(cls).not.toMatch(/#|rgb\(|hsl\(|dark:/);
    }
    expect(statusBadgeClass("success")).toContain("text-success");
    expect(statusBadgeClass("failed")).toContain("text-destructive");
  });
});

describe("simOutcomeClass", () => {
  it("maps sim outcomes to token foregrounds", () => {
    expect(simOutcomeClass("Success")).toBe("text-success");
    expect(simOutcomeClass("Fail")).toBe("text-destructive");
    expect(simOutcomeClass("Maintain")).toBe("text-warning");
  });
});
