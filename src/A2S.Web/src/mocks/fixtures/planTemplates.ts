/** Plan template previews for stories and tests: Plan v3 (Base/Build/Transition) and Lumbridge Lard Shed. */

import type { PlanTemplatePreview } from '@/api/planTemplates';
import { cutTo85Preview } from '@/mocks/handlers/suggestedPlans';

const tm = (key: string, label: string) => ({ key, label, unit: 'kg', default: null, minimum: 20, maximum: 400, optional: true });

export const planV3Preview: PlanTemplatePreview = {
  key: 'plan-v3',
  name: 'Plan v3 — Base/Build/Transition',
  description: 'Four lifting days in straight sets, A2S2 on bench, incline and front squat, pause squat 4 × 5–8; a stair finisher, walk → run behind its gate and evening mobility.',
  startDate: '2026-11-23',
  inputs: [
    tm('benchTm', 'Bench press TM'),
    tm('inclineSmithTm', 'Incline bench (Smith) TM'),
    tm('frontSquatTm', 'Front squat TM'),
    { key: 'startWeightKg', label: 'Start weight', unit: 'kg', default: 95, minimum: 30, maximum: 300 },
  ],
  schedule: [
    { day: 1, kind: 'Lift', label: 'Upper A' },
    { day: 1, kind: 'Mobility', label: 'Hips & lower back' },
    { day: 2, kind: 'Lift', label: 'Lower A' },
    { day: 2, kind: 'Mobility', label: 'Upper back & chest' },
    { day: 3, kind: 'Mobility', label: 'Full-body flow' },
    { day: 4, kind: 'Lift', label: 'Upper B' },
    { day: 5, kind: 'Lift', label: 'Lower B' },
    { day: 6, kind: 'Mobility', label: 'Full-body flow' },
    { day: 0, kind: 'Rest', label: 'Rest' },
  ],
  liftingDays: [],
  habits: ['Water', 'Morning weigh-in', 'Creatine'],
  checkpoints: [],
  alreadyStarted: false,
  phases: [
    { type: 'Cut', from: '2026-11-23', to: '2027-01-17', note: 'Base · weeks 1–8' },
    { type: 'Cut', from: '2027-01-18', to: '2027-03-14', note: 'Build · weeks 9–16' },
    { type: 'Maintenance', from: '2027-03-15', to: '2027-05-23', note: 'Transition · weeks 17–26' },
  ],
  sessions: [
    {
      label: 'Upper A',
      liftDay: 1,
      warmUp: 'Warm-up (upper)',
      finisher: 'Stair finisher',
      estimatedMinutes: 60,
      exercises: [
        { slot: '1', name: 'Bench Press (Barbell)', prescription: 'A2S2 · 5 sets', progression: 'A2S2 main lift', note: null },
        { slot: '2', name: 'Bent Over Row (Barbell)', prescription: '3 × 8–10', progression: 'Reps per set', note: null },
        { slot: '3', name: 'Single Arm Lateral Raise (Cable)', prescription: '2 × 12–15 each side', progression: 'Reps per set', note: 'Left side first' },
        { slot: '4', name: 'Single Arm Cable Row', prescription: '2 × 10–12 each side', progression: 'Reps per set', note: 'Left side first' },
        { slot: '5', name: 'Skullcrusher (Dumbbell)', prescription: '2 × 10–12', progression: 'Reps per set', note: null },
        { slot: '6', name: 'Bicep Curl (Dumbbell)', prescription: '2 × 10–12', progression: 'Reps per set', note: null },
        { slot: '7', name: 'Cable Core Palloff Press', prescription: '2 × 10 each side', progression: 'Reps per set', note: null },
      ],
    },
    {
      label: 'Lower A',
      liftDay: 2,
      warmUp: 'Warm-up (lower)',
      finisher: 'Treadmill · 10 min · Easy incline',
      estimatedMinutes: 55,
      exercises: [
        { slot: '1', name: 'Pause Squat (Barbell)', prescription: '4 × 5–8', progression: 'Reps per set', note: null },
        { slot: '2', name: 'Split Squat (Dumbbell)', prescription: '2 × 8–10 each side', progression: 'Reps per set', note: 'Left side first' },
        { slot: '3', name: 'Cable Twist', prescription: '2 × 10 each side', progression: 'Reps per set', note: null },
        { slot: '4', name: 'Lying Leg Curl (Machine)', prescription: '3 × 10–12', progression: 'Reps per set', note: null },
      ],
    },
  ],
  slots: [
    { day: 1, kind: 'Lift', label: 'Upper A', warmUp: 'Warm-up (upper)', finisher: 'Stair finisher', routine: null, track: null },
    { day: 1, kind: 'Mobility', label: 'Evening mobility', warmUp: null, finisher: null, routine: 'Hips & lower back', track: null },
    { day: 2, kind: 'Lift', label: 'Lower A', warmUp: 'Warm-up (lower)', finisher: 'Treadmill · 10 min · Easy incline', routine: null, track: null },
    { day: 2, kind: 'Mobility', label: 'Evening mobility', warmUp: null, finisher: null, routine: 'Upper back & chest', track: null },
    { day: 3, kind: 'Mobility', label: 'Mobility', warmUp: null, finisher: null, routine: 'Full-body flow', track: null },
    { day: 4, kind: 'Lift', label: 'Upper B', warmUp: 'Warm-up (upper)', finisher: null, routine: null, track: null },
    { day: 5, kind: 'Lift', label: 'Lower B', warmUp: 'Warm-up (lower)', finisher: null, routine: null, track: null },
    { day: 6, kind: 'Mobility', label: 'Mobility', warmUp: null, finisher: null, routine: 'Full-body flow', track: null },
    { day: 0, kind: 'Rest', label: 'Rest', warmUp: null, finisher: null, routine: null, track: null },
  ],
  routines: [
    {
      name: 'Hips & lower back',
      targetMinutes: 10,
      drills: [
        { name: '90/90 Hip Switch', dose: '2 × 6 each side', cues: 'Tall spine' },
        { name: 'Cat Camel', dose: '1 × 10', cues: null },
      ],
    },
    {
      name: 'Full-body flow',
      targetMinutes: 12,
      drills: [
        { name: 'Open Books', dose: '1 × 5 each side', cues: null },
        { name: "World's Greatest Stretch", dose: '1 × 5 each side', cues: null },
      ],
    },
  ],
  cardioTracks: [
    { name: 'Stair finisher', description: 'Ten minutes on the stairs after Upper A, stepping up a level when it is easy.' },
    { name: 'Walk → run', description: 'Couch to 5K once the running gate is passed: trend weight 90 kg or under, no knee, shin or Achilles pain for 4 weeks.' },
  ],
  toFillIn: [
    'Training max: Bench Press (Barbell) (optional now, required before its first session)',
    'Training max: Front Squat (optional now, required before its first session)',
    'Starting weights for 18 accessories (after the first session)',
  ],
  blockSequence: [1, 2, 3],
  programWeeks: 21,
};

export const cutTo85TemplatePreview: PlanTemplatePreview = {
  ...cutTo85Preview,
  phases: [
    { type: 'Cut', from: '2026-11-16', to: '2026-12-20', note: null },
    { type: 'Maintenance', from: '2026-12-21', to: '2027-01-03', note: null },
    { type: 'Cut', from: '2027-01-04', to: '2027-04-04', note: null },
  ],
  sessions: [],
  slots: [],
  routines: [],
  cardioTracks: [],
  toFillIn: [],
  blockSequence: [1, 2, 3],
  programWeeks: 21,
};

export const planTemplatesFixture: PlanTemplatePreview[] = [planV3Preview, cutTo85TemplatePreview];
