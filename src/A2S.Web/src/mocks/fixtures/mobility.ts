import { DoseUnit, DrillChangeKind, MobilityRoutineKind, SideBias, type DrillFeedbackResultDto, type MobilityRoutineDto } from '@/types/mobility';
import { MOBILITY_A_ID, MOBILITY_B_ID, MOBILITY_VIDEO_ID, MOBILITY_YOGA_ID } from './plan';

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

const ladderRange = { min: 15, max: 30, step: 5 };

/** A home yoga flow: a reverse plank ladder (3 levels) and a pose with Easier / Standard / Harder versions. */
export const homeYogaFixture: MobilityRoutineDto = {
  id: MOBILITY_YOGA_ID,
  name: 'Home yoga',
  targetMinutes: 20,
  kind: MobilityRoutineKind.YogaFlow,
  followAlongUrl: null,
  canUndo: false,
  recentChanges: [],
  drills: [
    {
      name: 'Reverse plank ladder',
      sets: 3,
      dose: { unit: DoseUnit.Seconds, amount: 25 },
      extraSetSide: SideBias.None,
      cues: null,
      standingAlternative: null,
      perSide: false,
      levels: [
        { name: 'Level 1', description: 'Reverse tabletop, knees bent', sets: 3, dose: { unit: DoseUnit.Seconds, amount: 15 }, range: ladderRange, cues: 'Hips high, squeeze glutes' },
        { name: 'Level 2', description: 'Hands on a bench, straight legs', sets: 3, dose: { unit: DoseUnit.Seconds, amount: 15 }, range: ladderRange, cues: 'Body in one line, chest proud' },
        { name: 'Level 3', description: 'Full reverse plank', sets: 3, dose: { unit: DoseUnit.Seconds, amount: 15 }, range: ladderRange, cues: 'Hands under shoulders, heels down' },
      ],
      currentLevel: 0,
      cleanStreak: 1,
      suggestedLevel: null,
    },
    {
      name: 'Low lunge',
      sets: 1,
      dose: { unit: DoseUnit.Breaths, amount: 5 },
      extraSetSide: SideBias.None,
      cues: null,
      standingAlternative: null,
      perSide: true,
      levels: [
        { name: 'Easier', description: 'Back knee padded, hands on blocks', sets: 1, dose: { unit: DoseUnit.Breaths, amount: 5 }, range: null, cues: 'Tuck the pelvis' },
        { name: 'Standard', description: 'Back knee down, arms up', sets: 1, dose: { unit: DoseUnit.Breaths, amount: 5 }, range: null, cues: 'Tuck the pelvis, ribs down' },
        { name: 'Harder', description: 'Back knee lifted', sets: 1, dose: { unit: DoseUnit.Breaths, amount: 5 }, range: null, cues: 'Long back leg' },
      ],
      currentLevel: 1,
      cleanStreak: 0,
      suggestedLevel: null,
    },
  ],
};

/** A follow-along video routine: just a link and the expected minutes. */
export const followAlongFixture: MobilityRoutineDto = {
  id: MOBILITY_VIDEO_ID,
  name: 'Hip flow video',
  targetMinutes: 25,
  kind: MobilityRoutineKind.FollowAlong,
  followAlongUrl: 'https://www.youtube.com/watch?v=example',
  drills: [],
};

/** What the server answers after a second clean session on the ladder: 25 s to 30 s. */
export function ladderStepUpResult(): DrillFeedbackResultDto {
  const routine = structuredClone(homeYogaFixture);
  routine.drills[0] = { ...routine.drills[0], dose: { unit: DoseUnit.Seconds, amount: 30 }, cleanStreak: 0 };
  const change = {
    changeId: 'chg-1',
    at: '2026-11-22T18:00:00Z',
    drillName: 'Reverse plank ladder',
    kind: DrillChangeKind.DoseIncreased,
    reason: '2 clean sessions: 25 s to 30 s',
    undone: false,
  };
  routine.recentChanges = [change];
  routine.canUndo = true;
  return { routine, changes: [change] };
}

/** At the top of Level 1 the server suggests Level 2. */
export function ladderLevelUpResult(): DrillFeedbackResultDto {
  const routine = structuredClone(homeYogaFixture);
  routine.drills[0] = { ...routine.drills[0], dose: { unit: DoseUnit.Seconds, amount: 30 }, cleanStreak: 0, suggestedLevel: 1 };
  const change = {
    changeId: 'chg-2',
    at: '2026-11-22T18:00:00Z',
    drillName: 'Reverse plank ladder',
    kind: DrillChangeKind.LevelUpSuggested,
    reason: '2 clean sessions at the top: ready for Level 2: Hands on a bench, straight legs',
    undone: false,
  };
  routine.recentChanges = [change];
  routine.canUndo = true;
  return { routine, changes: [change] };
}
