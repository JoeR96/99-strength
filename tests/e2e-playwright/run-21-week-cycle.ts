/**
 * Interactive E2E Playwright Script: 21-Week Monk Mode Cycle
 *
 * This script automates the full 21-week workout cycle through the browser UI,
 * with API-level assertions after each week for 3 tracked exercises:
 *
 * 1. OHP Smith Machine (Linear, Day 1) - TM=65kg
 * 2. Bent Over Row (bilateral RepsPerSet, Day 1) - startingSets=3, targetSets=5, weight=60kg
 * 3. Single Arm Lat Pulldown (unilateral RepsPerSet, Day 3) - startingSets=4, targetSets=6, weight=79kg
 *
 * Run: npx tsx tests/e2e-playwright/run-21-week-cycle.ts
 */

import { chromium, type Page, type Browser, type BrowserContext } from 'playwright';

// ─── Configuration ───────────────────────────────────────────────────────────

const FRONTEND_URL = 'http://localhost:5173';
const API_BASE_URL = 'https://localhost:5001/api/v1';
const TOTAL_WEEKS = 21;
const DAYS_PER_WEEK = 4;

// Test credentials (same as E2E test suite)
const TEST_EMAIL = 'Bigdave@gmail.com';
const TEST_PASSWORD = 'Tacomuncher123!';

// ─── Linear Progression Data (OHP Smith, TM=65kg) ───────────────────────────

// AMRAP deltas per week (same pattern as integration tests)
const weeklyDeltas = [
  +5, +4, +3, 0, -1, +5, 0,   // Block 1 (week 7 = deload)
  +4, +3, +5, -2, +4, +5, 0,  // Block 2 (week 14 = deload)
  +3, +4, 0, +5, -1, +4, 0    // Block 3 (week 21 = deload)
];

// Week intensity percentages - A2S2 Hypertrophy (1-indexed)
const intensities = [
  0, 0.65, 0.68, 0.70, 0.68, 0.70, 0.73, 0.60,
  0.68, 0.70, 0.73, 0.70, 0.73, 0.76, 0.60,
  0.70, 0.73, 0.76, 0.73, 0.76, 0.79, 0.60
];

// Reps per set for normal sets (1-indexed)
const repsPerSet = [
  0, 12, 11, 10, 11, 10, 9, 5, 11, 10, 9, 10, 9, 8, 5, 10, 9, 8, 9, 8, 7, 5
];

// Rep-out target (AMRAP baseline) per week (1-indexed, 0 = deload/no AMRAP)
const repOutTargets = [
  0, 15, 13, 12, 13, 12, 11, 0, 13, 12, 11, 12, 11, 10, 0, 12, 11, 10, 11, 10, 9, 0
];

// Sets per week - always 4 in hypertrophy (1-indexed)
const setsForWeek = [
  0, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4
];

/**
 * Rounds using banker's rounding (round-half-to-even) to match .NET's Math.Round behavior.
 * JavaScript's Math.round always rounds 0.5 up, but .NET rounds 0.5 to nearest even.
 */
function bankersRound(value: number): number {
  const rounded = Math.round(value);
  // Check if value is exactly at 0.5 midpoint
  if (Math.abs(value - (rounded - 0.5)) < 1e-10) {
    // At midpoint: round to even
    return rounded % 2 === 0 ? rounded : rounded - 1;
  }
  return rounded;
}

function calculateExpectedTmSequence(startingTm: number, deltas: number[]): number[] {
  const expected: number[] = new Array(22);
  expected[0] = startingTm;
  let tm = startingTm;

  for (let week = 1; week <= 21; week++) {
    const isDeload = week === 7 || week === 14 || week === 21;
    const delta = deltas[week - 1];

    if (!isDeload && delta !== 0) {
      let adjustmentPercent: number;
      if (delta >= 5) adjustmentPercent = 0.03;
      else if (delta === 4) adjustmentPercent = 0.02;
      else if (delta === 3) adjustmentPercent = 0.015;
      else if (delta === 2) adjustmentPercent = 0.01;
      else if (delta === 1) adjustmentPercent = 0.005;
      else if (delta === -1) adjustmentPercent = -0.02;
      else adjustmentPercent = -0.05; // delta <= -2

      const newVal = tm * (1 + adjustmentPercent);
      // TM is stored at full precision (2 decimal places), NOT rounded to gym increments.
      // Gym increment rounding only happens when calculating working weight.
      tm = Math.round(newVal * 100) / 100;
    }

    expected[week] = tm;
  }

  return expected;
}

// ─── Bilateral RepsPerSet Scenarios (Bent Over Row, Day 1) ───────────────────
// Barbell +2.5kg, startingSets=3, targetSets=5, maxSets=5 (bilateral)
// RepRange.Common.Medium: Min=8, Target=10, Max=12
// S=SUCCESS(12), M=MAINTAINED(10), F=FAILED(-1 means first set=7, rest=10)

const bentOverRowScenarios: { reps: number; expectedSets: number; expectedWeight: number; desc: string }[] = [
  { reps: 12, expectedSets: 4, expectedWeight: 60,   desc: "S: 3→4" },            // Week 1
  { reps: 12, expectedSets: 5, expectedWeight: 60,   desc: "S: 4→5" },            // Week 2
  { reps: 12, expectedSets: 3, expectedWeight: 62.5, desc: "S at target: +2.5kg, reset 3" }, // Week 3
  { reps: 10, expectedSets: 3, expectedWeight: 62.5, desc: "M: no change" },      // Week 4
  { reps: 12, expectedSets: 4, expectedWeight: 62.5, desc: "S: 3→4" },            // Week 5
  { reps: 12, expectedSets: 5, expectedWeight: 62.5, desc: "S: 4→5" },            // Week 6
  { reps: 12, expectedSets: 3, expectedWeight: 65,   desc: "S at target: +2.5kg, reset 3" }, // Week 7
  { reps: -1, expectedSets: 2, expectedWeight: 65,   desc: "F: 3→2" },            // Week 8
  { reps: 10, expectedSets: 2, expectedWeight: 65,   desc: "M: no change" },      // Week 9
  { reps: 12, expectedSets: 3, expectedWeight: 65,   desc: "S: 2→3" },            // Week 10
  { reps: 12, expectedSets: 4, expectedWeight: 65,   desc: "S: 3→4" },            // Week 11
  { reps: 12, expectedSets: 5, expectedWeight: 65,   desc: "S: 4→5" },            // Week 12
  { reps: 12, expectedSets: 3, expectedWeight: 67.5, desc: "S at target: +2.5kg, reset 3" }, // Week 13
  { reps: 10, expectedSets: 3, expectedWeight: 67.5, desc: "M: no change" },      // Week 14
  { reps: 12, expectedSets: 4, expectedWeight: 67.5, desc: "S: 3→4" },            // Week 15
  { reps: -1, expectedSets: 3, expectedWeight: 67.5, desc: "F: 4→3" },            // Week 16
  { reps: 12, expectedSets: 4, expectedWeight: 67.5, desc: "S: 3→4" },            // Week 17
  { reps: 12, expectedSets: 5, expectedWeight: 67.5, desc: "S: 4→5" },            // Week 18
  { reps: 12, expectedSets: 3, expectedWeight: 70,   desc: "S at target: +2.5kg, reset 3" }, // Week 19
  { reps: 10, expectedSets: 3, expectedWeight: 70,   desc: "M: no change" },      // Week 20
  { reps: 10, expectedSets: 3, expectedWeight: 70,   desc: "M: no change" },      // Week 21
];

// ─── Unilateral RepsPerSet Scenarios (Single Arm Lat Pulldown, Day 3) ────────
// Cable +2.5kg, startingSets=4, targetSets=6, maxSets=3 (unilateral)
// effectiveMaxSets = min(6,3) = 3. StartingSets(4) > effectiveMaxSets(3),
// so SUCCESS always increases weight and resets to 4 (startingSets).
// FAILED drops sets. Only when sets < 3 does SUCCESS add a set.

const latPulldownScenarios: { reps: number; expectedSets: number; expectedWeight: number; desc: string }[] = [
  { reps: 12, expectedSets: 4, expectedWeight: 81.5, desc: "S: 4>=3, +2.5kg, reset 4" },   // Week 1
  { reps: 12, expectedSets: 4, expectedWeight: 84,   desc: "S: 4>=3, +2.5kg, reset 4" },   // Week 2
  { reps: 10, expectedSets: 4, expectedWeight: 84,   desc: "M: no change" },                 // Week 3
  { reps: 12, expectedSets: 4, expectedWeight: 86.5, desc: "S: 4>=3, +2.5kg, reset 4" },   // Week 4
  { reps: -1, expectedSets: 3, expectedWeight: 86.5, desc: "F: 4→3" },                      // Week 5
  { reps: 10, expectedSets: 3, expectedWeight: 86.5, desc: "M: no change" },                 // Week 6
  { reps: 12, expectedSets: 4, expectedWeight: 89,   desc: "S: 3>=3, +2.5kg, reset 4" },   // Week 7
  { reps: -1, expectedSets: 3, expectedWeight: 89,   desc: "F: 4→3" },                      // Week 8
  { reps: -1, expectedSets: 2, expectedWeight: 89,   desc: "F: 3→2" },                      // Week 9
  { reps: 12, expectedSets: 3, expectedWeight: 89,   desc: "S: 2<3, add set 2→3" },         // Week 10
  { reps: 12, expectedSets: 4, expectedWeight: 91.5, desc: "S: 3>=3, +2.5kg, reset 4" },   // Week 11
  { reps: 10, expectedSets: 4, expectedWeight: 91.5, desc: "M: no change" },                 // Week 12
  { reps: 12, expectedSets: 4, expectedWeight: 94,   desc: "S: 4>=3, +2.5kg, reset 4" },   // Week 13
  { reps: 10, expectedSets: 4, expectedWeight: 94,   desc: "M: no change" },                 // Week 14
  { reps: 12, expectedSets: 4, expectedWeight: 96.5, desc: "S: 4>=3, +2.5kg, reset 4" },   // Week 15
  { reps: 10, expectedSets: 4, expectedWeight: 96.5, desc: "M: no change" },                 // Week 16
  { reps: 12, expectedSets: 4, expectedWeight: 99,   desc: "S: 4>=3, +2.5kg, reset 4" },   // Week 17
  { reps: -1, expectedSets: 3, expectedWeight: 99,   desc: "F: 4→3" },                      // Week 18
  { reps: 10, expectedSets: 3, expectedWeight: 99,   desc: "M: no change" },                 // Week 19
  { reps: 12, expectedSets: 4, expectedWeight: 101.5, desc: "S: 3>=3, +2.5kg, reset 4" },  // Week 20
  { reps: 10, expectedSets: 4, expectedWeight: 101.5, desc: "M: no change" },               // Week 21
];

// ─── Helper Functions ────────────────────────────────────────────────────────

async function getAuthToken(page: Page): Promise<string> {
  // Get auth token from Clerk in the browser context
  const token = await page.evaluate(async () => {
    // @ts-ignore - Clerk is available on window
    const clerk = window.Clerk;
    if (!clerk?.session) throw new Error('No Clerk session');
    return await clerk.session.getToken();
  });
  if (!token) throw new Error('Failed to get auth token');
  return token;
}

interface WorkoutDto {
  id: string;
  name: string;
  currentWeek: number;
  totalWeeks: number;
  variant: number;
  status: string;
  exercises: ExerciseDto[];
}

interface ExerciseDto {
  id: string;
  name: string;
  assignedDay: number;
  orderInDay: number;
  progression: {
    type: string;
    trainingMax?: { value: number; unit: number };
    useAmrap?: boolean;
    baseSetsPerExercise?: number;
    repRange?: { minimum: number; target: number; maximum: number };
    currentSetCount?: number;
    targetSets?: number;
    currentWeight?: number;
    weightUnit?: string;
    isUnilateral?: boolean;
  };
}

async function getCurrentWorkout(page: Page): Promise<WorkoutDto> {
  const token = await getAuthToken(page);
  const response = await page.evaluate(async ({ apiBase, authToken }) => {
    const resp = await fetch(`${apiBase}/workouts/current`, {
      headers: {
        'Authorization': `Bearer ${authToken}`,
        'Content-Type': 'application/json'
      }
    });
    if (!resp.ok) throw new Error(`API ${resp.status}: ${await resp.text()}`);
    return await resp.json();
  }, { apiBase: API_BASE_URL, authToken: token });
  return response as WorkoutDto;
}

async function sleep(ms: number) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

// ─── Login Flow ──────────────────────────────────────────────────────────────

async function loginOrSignUp(page: Page) {
  console.log('Navigating to sign-in...');
  await page.goto(`${FRONTEND_URL}/sign-in`);
  await page.waitForLoadState('networkidle');

  // Wait for Clerk's sign-in component to load
  await page.waitForSelector('.cl-card, .cl-rootBox, [data-clerk-component], #clerk-components', { timeout: 30000 });

  // Step 1: Enter email
  const emailInput = page.locator('#identifier-field').first();
  await emailInput.waitFor({ state: 'visible', timeout: 15000 });
  await emailInput.fill(TEST_EMAIL);

  // Click Continue to proceed to password step
  const continueBtn = page.locator('button:has-text("Continue")').first();
  await continueBtn.click();

  // Step 2: Wait for password field and enter password
  const passwordInput = page.locator('#password-field').first();
  await passwordInput.waitFor({ state: 'visible', timeout: 15000 });
  await passwordInput.fill(TEST_PASSWORD);

  // Click Continue to sign in
  const signInBtn = page.locator('button:has-text("Continue")').first();
  await signInBtn.click();

  // Wait for dashboard redirect
  await page.waitForURL(url => url.toString().includes('/dashboard') || url.toString().includes('/workout'), { timeout: 30000 });
  console.log('Logged in successfully!');
}

// ─── Setup Wizard Flow ───────────────────────────────────────────────────────

async function createMonkModeWorkout(page: Page) {
  console.log('🏋️ Creating Monk Mode workout...');

  // Navigate to workout page to check if one exists
  await page.goto(`${FRONTEND_URL}/workout`);
  await page.waitForLoadState('networkidle');
  await sleep(2000);

  // Check if workout already exists
  const noWorkoutHeading = page.locator('h2:has-text("No Active Workout")').first();
  const hasNoWorkout = await noWorkoutHeading.isVisible().catch(() => false);

  if (!hasNoWorkout) {
    // Always delete existing workout to start fresh
    console.log('  Deleting existing workout to start fresh...');
    const workout = await getCurrentWorkout(page);
    const token = await getAuthToken(page);
    await page.evaluate(async ({ apiBase, authToken, workoutId }) => {
      await fetch(`${apiBase}/workouts/${workoutId}`, {
        method: 'DELETE',
        headers: { 'Authorization': `Bearer ${authToken}` }
      });
    }, { apiBase: API_BASE_URL, authToken: token, workoutId: workout.id });
    await sleep(1000);
    await page.reload();
    await page.waitForLoadState('networkidle');
    await sleep(2000);
  }

  // Click "Create Workout Program"
  const createBtn = page.locator('button:has-text("Create Workout Program")').first();
  await createBtn.waitFor({ state: 'visible', timeout: 10000 });
  await createBtn.click();

  // Wait for setup page
  await page.waitForURL(url => url.toString().includes('/setup'), { timeout: 10000 });
  await sleep(1000);

  // Step 1: Select "Start from Template"
  console.log('  Step 1: Selecting template mode...');
  const templateOption = page.locator('text=Start from Template').first();
  await templateOption.waitFor({ state: 'visible', timeout: 5000 });
  await templateOption.click();
  await sleep(500);

  // Click Next
  const nextBtn = page.locator('button:has-text("Next")').first();
  await nextBtn.click();
  await sleep(1000);

  // Step 2: Select Monk Mode template
  console.log('  Step 2: Selecting Monk Mode template...');
  const monkModeCard = page.locator('text=Monk Mode').first();
  await monkModeCard.waitFor({ state: 'visible', timeout: 5000 });
  await monkModeCard.click();
  await sleep(500);

  // Click Next
  await nextBtn.click();
  await sleep(1000);

  // Step 3: Review exercises (skip, pre-populated)
  console.log('  Step 3: Reviewing exercises (pre-populated)...');
  await sleep(500);
  await nextBtn.click();
  await sleep(1000);

  // Step 4: Confirm and create
  console.log('  Step 4: Creating program...');
  const createProgramBtn = page.locator('button:has-text("Create Program")').first();
  const isCreateVisible = await createProgramBtn.isVisible().catch(() => false);
  if (isCreateVisible) {
    await createProgramBtn.click();
  } else {
    // May still need to click Next once more
    await nextBtn.click();
    await sleep(500);
    await createProgramBtn.waitFor({ state: 'visible', timeout: 5000 });
    await createProgramBtn.click();
  }

  // Wait for redirect
  await page.waitForURL(
    url => url.toString().includes('/dashboard') || url.toString().includes('/workout'),
    { timeout: 15000 }
  );
  await sleep(2000);

  console.log('✅ Monk Mode workout created!');
}

// ─── Workout Completion Flow ─────────────────────────────────────────────────

async function completeWorkoutDay(page: Page, day: number, week: number): Promise<void> {
  // Navigate to workout overview
  await page.goto(`${FRONTEND_URL}/workout`);
  await page.waitForLoadState('networkidle');
  await sleep(1500);

  // Click "Start Workout" for this day
  const startBtn = page.locator(`[data-testid='start-workout-day-${day}']`).first();
  await startBtn.waitFor({ state: 'visible', timeout: 10000 });

  const isEnabled = await startBtn.isEnabled();
  if (!isEnabled) {
    throw new Error(`Day ${day} button is disabled - may already be completed`);
  }

  await startBtn.click();

  // Wait for session page
  await page.waitForURL(url => url.toString().includes(`/workout/session/${day}`), { timeout: 10000 });
  await sleep(1500);

  // Get the workout data to know what exercises and sets to fill
  const workout = await getCurrentWorkout(page);
  const dayExercises = workout.exercises
    .filter(e => e.assignedDay === day)
    .sort((a, b) => a.orderInDay - b.orderInDay);

  // Complete each exercise
  for (const exercise of dayExercises) {
    await completeExercise(page, exercise, week, day);
  }

  // Click "Complete Workout"
  const completeBtn = page.locator("[data-testid='complete-workout-button']").first();
  await completeBtn.waitFor({ state: 'visible', timeout: 5000 });
  await completeBtn.click();

  // Wait for completion screen - use a single CSS selector that matches either element
  // The final day (program complete) may take longer due to additional server processing
  const completionTimeout = (week === TOTAL_WEEKS && day === DAYS_PER_WEEK) ? 45000 : 20000;
  await page.waitForSelector(
    "[data-testid='completion-title']",
    { state: 'visible', timeout: completionTimeout }
  );

  // Click continue/back button
  await sleep(1000);
  const continueBtn = page.locator("[data-testid='continue-button']").first();
  const backBtn = page.locator("[data-testid='back-to-workout-button']").first();

  const hasContinue = await continueBtn.isVisible().catch(() => false);
  const hasBack = await backBtn.isVisible().catch(() => false);

  if (hasContinue) {
    await continueBtn.click();
  } else if (hasBack) {
    await backBtn.click();
  } else {
    // On final day completion, just navigate away
    await page.goto(`${FRONTEND_URL}/workout`);
  }

  // Wait for navigation back
  try {
    await page.waitForURL(
      url => url.toString().includes('/workout') && !url.toString().includes('/session'),
      { timeout: 10000 }
    );
  } catch {
    // Final day may redirect to dashboard or show program complete
    await page.goto(`${FRONTEND_URL}/workout`);
  }
  await sleep(500);
}

async function completeExercise(page: Page, exercise: ExerciseDto, week: number, day: number): Promise<void> {
  const prog = exercise.progression;
  // Match the exact slug logic from WorkoutSession.tsx: name.replace(/\s+/g, "-").toLowerCase()
  const exerciseNameSlug = exercise.name.replace(/\s+/g, '-').toLowerCase();
  const card = page.locator(`[data-testid='exercise-card-${exerciseNameSlug}']`).first();

  // Verify the exercise card exists
  const cardVisible = await card.isVisible().catch(() => false);
  if (!cardVisible) {
    // Try scrolling down to find it
    await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight));
    await sleep(300);
  }

  // Determine how many sets and what reps to enter
  let numSets: number;
  let repsPerNormalSet: number;
  let amrapReps: number | null = null;
  let isLinear = false;

  // Count actual set rows displayed in the UI for this exercise
  const setRows = card.locator("[data-testid^='set-row-']");
  const actualSetCount = await setRows.count();

  if (prog.type === 'Linear') {
    isLinear = true;
    numSets = actualSetCount; // Use what the UI actually shows
    repsPerNormalSet = repsPerSet[week];
    const repOutTarget = repOutTargets[week];
    const isDeload = week === 7 || week === 14 || week === 21;
    const delta = weeklyDeltas[week - 1];

    if (exercise.name === 'Overhead Press (Smith Machine)') {
      // AMRAP reps = repOutTarget + delta (repOutTarget is the AMRAP baseline)
      amrapReps = isDeload ? repsPerNormalSet : Math.max(1, repOutTarget + delta);
    } else if (exercise.name === 'Squat (Barbell)') {
      // Maintain performance for other linear exercises
      amrapReps = repOutTarget || repsPerNormalSet; // delta=0
    } else if (exercise.name === 'Pause Squat (Barbell)') {
      amrapReps = repOutTarget || repsPerNormalSet; // delta=0
    }
  } else if (prog.type === 'RepsPerSet') {
    numSets = actualSetCount; // Use what the UI actually shows

    // Determine reps based on scenario
    if (exercise.name === 'Bent Over Row (Barbell)') {
      const scenario = bentOverRowScenarios[week - 1];
      repsPerNormalSet = scenario.reps === -1 ? 10 : scenario.reps;
      // For FAILED (-1): first set = 7, rest = 10
    } else if (exercise.name === 'Single Arm Lat Pulldown') {
      const scenario = latPulldownScenarios[week - 1];
      repsPerNormalSet = scenario.reps === -1 ? 10 : scenario.reps;
    } else {
      // All other exercises: MAINTAINED (reps = target = 10)
      repsPerNormalSet = 10;
    }
  } else {
    // MinimalSets or unknown - maintain
    numSets = actualSetCount;
    repsPerNormalSet = 10;
  }

  // Fill each set
  for (let setNum = 1; setNum <= numSets; setNum++) {
    const repsInput = card.locator(`[data-testid='reps-input-${setNum}']`);
    const hasRepsInput = await repsInput.isVisible().catch(() => false);

    if (hasRepsInput) {
      let repsToEnter: number;

      if (isLinear && setNum === numSets && amrapReps !== null) {
        // Last set is AMRAP for linear
        repsToEnter = amrapReps;
      } else if (prog.type === 'RepsPerSet') {
        // Handle FAILED scenario: first set below min
        if (exercise.name === 'Bent Over Row (Barbell)') {
          const scenario = bentOverRowScenarios[week - 1];
          if (scenario.reps === -1) {
            repsToEnter = setNum === 1 ? 7 : 10;
          } else {
            repsToEnter = scenario.reps;
          }
        } else if (exercise.name === 'Single Arm Lat Pulldown') {
          const scenario = latPulldownScenarios[week - 1];
          if (scenario.reps === -1) {
            repsToEnter = setNum === 1 ? 7 : 10;
          } else {
            repsToEnter = scenario.reps;
          }
        } else {
          repsToEnter = repsPerNormalSet;
        }
      } else {
        repsToEnter = repsPerNormalSet;
      }

      await repsInput.fill(repsToEnter.toString());
    }

    // Log details for tracked exercises
    const isTracked = ['Overhead Press (Smith Machine)', 'Bent Over Row (Barbell)', 'Single Arm Lat Pulldown'].includes(exercise.name);
    if (isTracked) {
      const currentRepsValue = hasRepsInput ? await repsInput.inputValue() : 'N/A';
      const weightInput = card.locator(`[data-testid='weight-input-${setNum}']`);
      const currentWeightValue = await weightInput.isVisible().catch(() => false) ? await weightInput.inputValue() : 'N/A';
      console.log(`      ${exercise.name} set ${setNum}: weight=${currentWeightValue}, reps=${currentRepsValue}`);
    }

    // Click "Log" button
    const completeSetBtn = card.locator(`[data-testid='complete-set-${setNum}']`);
    await completeSetBtn.click();
    await sleep(100);
  }
}

// ─── Assertion Functions ─────────────────────────────────────────────────────

function assertEqual(actual: number, expected: number, message: string) {
  if (Math.abs(actual - expected) > 0.01) {
    throw new Error(`ASSERTION FAILED: ${message}\n  Expected: ${expected}\n  Actual:   ${actual}`);
  }
}

async function assertProgressionState(page: Page, week: number, expectedTmValues: number[]): Promise<void> {
  const workout = await getCurrentWorkout(page);

  // 1. Assert Linear (OHP Smith) Training Max
  const ohp = workout.exercises.find(e => e.name === 'Overhead Press (Smith Machine)')!;
  const ohpTm = ohp.progression.trainingMax!.value;
  const expectedTm = expectedTmValues[week];
  assertEqual(ohpTm, expectedTm, `Week ${week} OHP TM: delta=${weeklyDeltas[week - 1]}`);
  console.log(`    ✓ OHP TM = ${ohpTm}kg (expected ${expectedTm}kg)`);

  // 2. Assert Bilateral RepsPerSet (Bent Over Row)
  const row = workout.exercises.find(e => e.name === 'Bent Over Row (Barbell)')!;
  const rowSets = row.progression.currentSetCount!;
  const rowWeight = row.progression.currentWeight!;
  const rowScenario = bentOverRowScenarios[week - 1];
  assertEqual(rowSets, rowScenario.expectedSets, `Week ${week} Bent Over Row sets: ${rowScenario.desc}`);
  assertEqual(rowWeight, rowScenario.expectedWeight, `Week ${week} Bent Over Row weight: ${rowScenario.desc}`);
  console.log(`    ✓ Bent Over Row = ${rowSets} sets × ${rowWeight}kg (expected ${rowScenario.expectedSets}×${rowScenario.expectedWeight}kg - ${rowScenario.desc})`);

  // 3. Assert Unilateral RepsPerSet (Single Arm Lat Pulldown)
  const lat = workout.exercises.find(e => e.name === 'Single Arm Lat Pulldown')!;
  const latSets = lat.progression.currentSetCount!;
  const latWeight = lat.progression.currentWeight!;
  const latScenario = latPulldownScenarios[week - 1];
  assertEqual(latSets, latScenario.expectedSets, `Week ${week} Lat Pulldown sets: ${latScenario.desc}`);
  assertEqual(latWeight, latScenario.expectedWeight, `Week ${week} Lat Pulldown weight: ${latScenario.desc}`);
  console.log(`    ✓ Single Arm Lat Pulldown = ${latSets} sets × ${latWeight}kg (expected ${latScenario.expectedSets}×${latScenario.expectedWeight}kg - ${latScenario.desc})`);

  // 4. Assert unilateral flag
  if (!lat.progression.isUnilateral) {
    throw new Error(`Week ${week}: Single Arm Lat Pulldown should be marked as unilateral`);
  }
}

// ─── Main Execution ──────────────────────────────────────────────────────────

async function main() {
  console.log('═══════════════════════════════════════════════════════════');
  console.log('  A2S Monk Mode - 21-Week Interactive E2E Cycle');
  console.log('═══════════════════════════════════════════════════════════\n');

  const expectedTmValues = calculateExpectedTmSequence(65, weeklyDeltas);
  console.log('Expected OHP TM sequence:');
  for (let w = 0; w <= 21; w++) {
    console.log(`  ${w === 0 ? 'Start' : `Week ${w.toString().padStart(2)}`}: ${expectedTmValues[w]}kg`);
  }
  console.log('');

  let browser: Browser | null = null;
  let context: BrowserContext | null = null;

  try {
    // Launch headed browser
    browser = await chromium.launch({
      headless: false,
      slowMo: 50, // Slow down for visibility
    });

    context = await browser.newContext({
      ignoreHTTPSErrors: true, // For localhost HTTPS
      viewport: { width: 1280, height: 800 },
    });

    const page = await context.newPage();

    // Step 1: Login
    await loginOrSignUp(page);

    // Step 2: Create Monk Mode workout
    await createMonkModeWorkout(page);

    // Verify initial state
    const initialWorkout = await getCurrentWorkout(page);
    console.log(`\n📊 Initial workout state:`);
    console.log(`  Name: ${initialWorkout.name}`);
    console.log(`  Week: ${initialWorkout.currentWeek}/${initialWorkout.totalWeeks}`);
    console.log(`  Exercises: ${initialWorkout.exercises.length}`);
    console.log('');

    // Step 3: Complete all 84 workout days
    let totalAssertionsPassed = 0;

    for (let week = 1; week <= TOTAL_WEEKS; week++) {
      const isDeload = week === 7 || week === 14 || week === 21;
      console.log(`\n${'═'.repeat(60)}`);
      console.log(`  WEEK ${week}/${TOTAL_WEEKS} ${isDeload ? '(DELOAD)' : ''}`);
      console.log(`${'═'.repeat(60)}`);

      for (let day = 1; day <= DAYS_PER_WEEK; day++) {
        console.log(`\n  📋 Day ${day} of Week ${week}...`);
        await completeWorkoutDay(page, day, week);
        console.log(`  ✅ Day ${day} completed`);
      }

      // Assert progression state after completing the week
      if (week < TOTAL_WEEKS) {
        console.log(`\n  📊 Asserting progression state after Week ${week}:`);
        await assertProgressionState(page, week, expectedTmValues);
        totalAssertionsPassed += 3; // 3 exercises asserted per week
        console.log(`  ✅ All assertions passed for Week ${week}`);
      } else {
        console.log(`\n  🏁 Week 21 complete - workout is now finished!`);
      }
    }

    console.log('\n' + '═'.repeat(60));
    console.log('  🎉 21-WEEK CYCLE COMPLETE!');
    console.log(`  Total assertions passed: ${totalAssertionsPassed}`);
    console.log(`  Total workout days completed: ${TOTAL_WEEKS * DAYS_PER_WEEK}`);
    console.log('═'.repeat(60));

    // Keep browser open for manual inspection
    console.log('\n⏳ Browser staying open for inspection. Press Ctrl+C to close.');
    await sleep(300000); // 5 minutes

  } catch (error) {
    console.error('\n❌ ERROR:', error);
    // Keep browser open on error for debugging
    console.log('\n⏳ Browser staying open for debugging. Press Ctrl+C to close.');
    if (browser) await sleep(300000);
    throw error;
  } finally {
    if (context) await context.close();
    if (browser) await browser.close();
  }
}

main().catch(err => {
  console.error('Fatal error:', err);
  process.exit(1);
});
