/**
 * Mobility routine types mirroring `A2S.Application.DTOs.PlanDtos` (`MobilityRoutineDto`,
 * `MobilityDrillDto`, `DrillDoseDto`). Enums serialise as integers (System.Text.Json default).
 */

export const DoseUnit = { Reps: 0, Seconds: 1, Breaths: 2 } as const;
export type DoseUnit = (typeof DoseUnit)[keyof typeof DoseUnit];

export const SideBias = { None: 0, Left: 1, Right: 2 } as const;
export type SideBias = (typeof SideBias)[keyof typeof SideBias];

export interface DrillDoseDto {
  unit: DoseUnit;
  amount: number;
}

export interface MobilityDrillDto {
  name: string;
  sets: number;
  dose: DrillDoseDto;
  /** The side that gets one extra set (left-side compensation work). */
  extraSetSide: SideBias;
  cues: string | null;
  standingAlternative: string | null;
  /** The dose is done on each side. */
  perSide: boolean;
}

export interface MobilityRoutineDto {
  id: string;
  name: string;
  targetMinutes: number;
  drills: MobilityDrillDto[];
}

/** PUT `/mobility-routines/{id}`: rename and change target minutes. */
export interface UpdateMobilityRoutineRequest {
  name: string;
  targetMinutes: number;
}

/** Body for `POST /mobility-routines`. */
export interface CreateMobilityRoutineRequest {
  name: string;
  targetMinutes: number;
  drills: MobilityDrillDto[];
}

export const doseUnitLabel: Record<DoseUnit, { one: string; many: string }> = {
  [DoseUnit.Reps]: { one: 'rep', many: 'reps' },
  [DoseUnit.Seconds]: { one: 'second', many: 'seconds' },
  [DoseUnit.Breaths]: { one: 'breath', many: 'breaths' },
};

export const sideBiasLabel: Record<SideBias, string> = {
  [SideBias.None]: 'No extra set',
  [SideBias.Left]: 'Extra set left',
  [SideBias.Right]: 'Extra set right',
};
