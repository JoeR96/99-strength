/** Story/test fixtures for cardio tracks: a walk builder, a walk → run waiting on a decision, and a stair finisher. */

import {
  CardioLogSource,
  CardioModality,
  CardioTrackStatus,
  EffortRating,
  IntensityUnit,
  PainFlag,
  TrackEffortTarget,
  TrackOutcome,
  TrackStageKind,
  TrackSuggestion,
  VolumeUnit,
  type CardioTrackDetailDto,
  type CardioTrackDto,
  type CardioTrackTemplateDto,
  type TodayCardioDto,
  type TrackStageDto,
} from '@/types/cardioTracks';

export const WALK_TRACK_ID = '6a1e0000-0000-4000-8000-000000000001';
export const RUN_TRACK_ID = '6a1e0000-0000-4000-8000-000000000002';
export const STAIR_TRACK_ID = '6a1e0000-0000-4000-8000-000000000003';

const SAFETY = 'Build up gradually, and stop for chest pain, dizziness or unusual breathlessness.';

const minutesStage = (
  name: string,
  target: TrackEffortTarget,
  min: number,
  max: number,
  step: number,
  intensity: IntensityUnit,
  start: number | null,
  top: number | null,
  extra: Partial<TrackStageDto> = {}
): TrackStageDto => ({
  name,
  kind: TrackStageKind.DoubleProgression,
  target,
  weekProgram: null,
  doubleProgression: {
    volumeUnit: VolumeUnit.Minutes,
    volumeMin: min,
    volumeMax: max,
    volumeStep: step,
    volumeStepPercent: null,
    intensityUnit: intensity,
    intensityStart: start,
    intensityStep: start === null ? null : 1,
    intensityMax: top,
    sessionsToAdvance: 2,
    workSeconds: null,
    restSeconds: null,
  },
  hrCeilingZone: target === TrackEffortTarget.Easy ? 2 : null,
  zoneTargetFraction: target === TrackEffortTarget.Hard ? 0.5 : null,
  note: null,
  ...extra,
});

const state = (overrides: Partial<CardioTrackDto['state']> = {}): CardioTrackDto['state'] => ({
  stageIndex: 0,
  week: 0,
  volume: 20,
  intensity: null,
  qualifying: 0,
  consecutiveMisses: 0,
  weekSessions: 0,
  stageComplete: false,
  suggestion: TrackSuggestion.None,
  step: 0,
  ...overrides,
});

const prescription = (title: string, minutes: number, stageName: string, overrides: Partial<CardioTrackDto['prescription']> = {}) => ({
  title,
  stageName,
  minutes,
  workMinutes: minutes,
  volumeUnit: VolumeUnit.Minutes,
  volume: minutes,
  intensityUnit: IntensityUnit.None,
  intensity: null,
  workSeconds: null,
  restSeconds: null,
  week: null,
  note: null,
  ...overrides,
});

export const walkTrack: CardioTrackDto = {
  id: WALK_TRACK_ID,
  name: 'Walk builder',
  modality: CardioModality.Walk,
  status: CardioTrackStatus.Active,
  templateKey: 'walk-builder',
  notes: 'Treadmill incline walking.',
  startedOn: '2026-11-02',
  isHard: false,
  stages: [
    minutesStage('Build duration', TrackEffortTarget.Easy, 20, 45, 5, IntensityUnit.None, null, null, { note: 'Easy: you can talk in full sentences.' }),
    minutesStage('Add incline', TrackEffortTarget.Easy, 45, 45, 5, IntensityUnit.InclinePercent, 1, 10),
  ],
  state: state({ volume: 25, qualifying: 1, step: 1 }),
  prescription: prescription('25 min continuous', 25, 'Build duration', { note: 'Easy: you can talk in full sentences.' }),
  sessionsToStepUp: 1,
  suggestion: TrackSuggestion.None,
  suggestionText: null,
  canUndo: true,
  benchmark: { name: 'Avg HR, 30 min at a fixed pace', unit: 'bpm', higherIsBetter: false, everyWeeks: 4 },
  benchmarkDue: false,
  safetyNote: SAFETY,
};

export const runTrack: CardioTrackDto = {
  id: RUN_TRACK_ID,
  name: 'Walk → run',
  modality: CardioModality.Run,
  status: CardioTrackStatus.Active,
  templateKey: 'walk-to-run',
  notes: 'Treadmill or grass preferred.',
  startedOn: '2026-11-02',
  isHard: false,
  stages: [
    {
      name: 'Couch to 5K',
      kind: TrackStageKind.WeekProgram,
      target: TrackEffortTarget.Easy,
      weekProgram: {
        weeks: Array.from({ length: 9 }, (_, i) => ({ number: i + 1, label: `C25K wk ${i + 1}`, minutes: 30, description: 'Runs with walking between' })),
        sessionsToAdvance: 3,
        sessionsPerWeek: 2,
      },
      doubleProgression: null,
      hrCeilingZone: null,
      zoneTargetFraction: null,
      note: null,
    },
    minutesStage('5 K builder', TrackEffortTarget.Easy, 30, 35, 1, IntensityUnit.None, null, null, { hrCeilingZone: null }),
  ],
  state: state({ week: 2, volume: 0, weekSessions: 3, suggestion: TrackSuggestion.RepeatWeek, step: 1 }),
  prescription: prescription('C25K wk 2 day 4', 31, 'Couch to 5K', { volumeUnit: null, volume: null, week: 2, note: 'Runs with walking between' }),
  sessionsToStepUp: 0,
  suggestion: TrackSuggestion.RepeatWeek,
  suggestionText: 'Not every session this week counted. Repeat the week?',
  canUndo: true,
  benchmark: { name: '5 K time', unit: 'min', higherIsBetter: false, everyWeeks: 4 },
  benchmarkDue: true,
  safetyNote: SAFETY,
};

export const stairTrack: CardioTrackDto = {
  id: STAIR_TRACK_ID,
  name: 'Stair finisher',
  modality: CardioModality.StairClimber,
  status: CardioTrackStatus.Active,
  templateKey: 'stair-finisher',
  notes: 'Finisher for upper-body days.',
  startedOn: '2026-11-02',
  isHard: true,
  stages: [
    minutesStage('Continuous', TrackEffortTarget.Hard, 5, 10, 1, IntensityUnit.Level, 6, 9),
    {
      ...minutesStage('Intervals', TrackEffortTarget.Hard, 4, 8, 1, IntensityUnit.Level, 10, null),
      doubleProgression: {
        ...minutesStage('Intervals', TrackEffortTarget.Hard, 4, 8, 1, IntensityUnit.Level, 10, null).doubleProgression!,
        volumeUnit: VolumeUnit.Rounds,
        workSeconds: 60,
        restSeconds: 60,
      },
    },
  ],
  state: state({ volume: 7, intensity: 6, qualifying: 0, step: 2 }),
  prescription: prescription('7 min continuous @ level 6', 7, 'Continuous', { intensityUnit: IntensityUnit.Level, intensity: 6 }),
  sessionsToStepUp: 2,
  suggestion: TrackSuggestion.None,
  suggestionText: null,
  canUndo: false,
  benchmark: { name: 'Floors in 5 min, all out', unit: 'floors', higherIsBetter: true, everyWeeks: 4 },
  benchmarkDue: false,
  safetyNote: SAFETY,
};

export const cardioTracksFixture: CardioTrackDto[] = [walkTrack, runTrack, stairTrack];

export const cardioTemplatesFixture: CardioTrackTemplateDto[] = [
  { key: 'walk-builder', name: 'Walk builder', modality: CardioModality.Walk, description: 'Easy zone 2 walking: 20 → 45 min, then incline.' },
  { key: 'walk-to-run', name: 'Walk → run', modality: CardioModality.Run, description: 'Couch to 5K, then a 5 K builder up to 35 min.' },
  { key: 'stair-finisher', name: 'Stair finisher', modality: CardioModality.StairClimber, description: 'After lifting: 5 → 10 min, then 1:1 intervals.' },
];

export const todayCardioFixture: TodayCardioDto = {
  tracks: cardioTracksFixture,
  highIntensityMinutesThisWeek: 34,
  warnings: [{ code: 'HighIntensityAboveMax', message: '34 min in zone 4–5 this week, above your 30 min max. Keep the rest easy.' }],
};

const session = (date: string, title: string, outcome: TrackOutcome, minutes: number, floors: number | null, level: number | null) => ({
  key: `manual:${date}`,
  date,
  prescription: title,
  stageIndex: 0,
  completed: true,
  qualified: outcome !== TrackOutcome.Held,
  outcome,
  durationSeconds: minutes * 60,
  effort: EffortRating.Brutal,
  rpe: 9,
  pain: PainFlag.None,
  painArea: null,
  avgHeartRate: 162,
  highIntensityMinutes: Math.ceil(minutes * 0.6),
  zone5Minutes: 1,
  floors,
  distanceMeters: null,
  intensityValue: level,
  source: CardioLogSource.Manual,
});

export const stairDetailFixture: CardioTrackDetailDto = {
  track: stairTrack,
  sessions: [
    session('2026-11-03', '5 min continuous @ level 6', TrackOutcome.Qualified, 5, 28, 6),
    session('2026-11-05', '5 min continuous @ level 6', TrackOutcome.SteppedUp, 5, 30, 6),
    session('2026-11-10', '6 min continuous @ level 6', TrackOutcome.Held, 6, 31, 6),
    session('2026-11-12', '6 min continuous @ level 6', TrackOutcome.Qualified, 6, 35, 6),
    session('2026-11-17', '6 min continuous @ level 6', TrackOutcome.SteppedUp, 6, 36, 6),
  ],
  audit: [
    { atUtc: '2026-11-05T18:00:00Z', date: '2026-11-05', action: 'SteppedUp', reason: 'Completed with enough zone 4–5 minutes: 2 qualifying sessions, volume up', before: '5 min continuous @ level 6', after: '6 min continuous @ level 6' },
    { atUtc: '2026-11-10T18:00:00Z', date: '2026-11-10', action: 'Held', reason: 'Not enough time in zone 4–5: holding', before: '6 min continuous @ level 6', after: '6 min continuous @ level 6' },
    { atUtc: '2026-11-17T18:00:00Z', date: '2026-11-17', action: 'SteppedUp', reason: 'Completed with enough zone 4–5 minutes: 2 qualifying sessions, volume up', before: '6 min continuous @ level 6', after: '7 min continuous @ level 6' },
  ],
  benchmarks: [{ date: '2026-11-02', value: 30, notes: null, isPersonalBest: true }],
  chart: [
    { date: '2026-11-03', step: 0, workMinutes: 5, intensity: 6, avgHeartRate: 162, highIntensityMinutes: 3, prescription: '5 min continuous @ level 6' },
    { date: '2026-11-05', step: 1, workMinutes: 5, intensity: 6, avgHeartRate: 162, highIntensityMinutes: 3, prescription: '5 min continuous @ level 6' },
    { date: '2026-11-10', step: 1, workMinutes: 6, intensity: 6, avgHeartRate: 160, highIntensityMinutes: 2, prescription: '6 min continuous @ level 6' },
    { date: '2026-11-12', step: 1, workMinutes: 6, intensity: 6, avgHeartRate: 163, highIntensityMinutes: 4, prescription: '6 min continuous @ level 6' },
    { date: '2026-11-17', step: 2, workMinutes: 6, intensity: 6, avgHeartRate: 164, highIntensityMinutes: 4, prescription: '6 min continuous @ level 6' },
  ],
};

export function detailFor(id: string): CardioTrackDetailDto {
  const track = cardioTracksFixture.find((t) => t.id === id) ?? stairTrack;
  return id === STAIR_TRACK_ID ? stairDetailFixture : { track, sessions: [], audit: [], benchmarks: [], chart: [] };
}
