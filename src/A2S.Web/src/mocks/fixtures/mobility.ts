import { DoseUnit, SideBias, type MobilityRoutineDto } from '@/types/mobility';
import { MOBILITY_A_ID, MOBILITY_B_ID } from './plan';

/** Two routines; Mobility B matches the mobility session in `todayCardio`. */
export const mobilityRoutinesFixture: MobilityRoutineDto[] = [
  {
    id: MOBILITY_A_ID,
    name: 'Mobility A',
    targetMinutes: 10,
    drills: [
      { name: '90/90 breathing', sets: 2, dose: { unit: DoseUnit.Breaths, amount: 5 }, extraSetSide: SideBias.None, cues: 'Ribs down, jaw loose', standingAlternative: 'Back-to-wall breathing', perSide: false },
      { name: 'Open books', sets: 2, dose: { unit: DoseUnit.Reps, amount: 5 }, extraSetSide: SideBias.Left, cues: null, standingAlternative: 'Side-on wall rotation', perSide: true },
    ],
  },
  {
    id: MOBILITY_B_ID,
    name: 'Mobility B',
    targetMinutes: 25,
    drills: [
      { name: 'Half-kneeling hip flexor stretch', sets: 2, dose: { unit: DoseUnit.Seconds, amount: 30 }, extraSetSide: SideBias.Left, cues: 'Squeeze the back glute', standingAlternative: null, perSide: true },
      { name: 'Cat-cow', sets: 1, dose: { unit: DoseUnit.Reps, amount: 8 }, extraSetSide: SideBias.None, cues: null, standingAlternative: null, perSide: false },
    ],
  },
];
