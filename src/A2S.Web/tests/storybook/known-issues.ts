// Mobile defects the page stories have today (found by tests/storybook/pages.spec.ts on a
// 390 × 844 viewport against the Storybook build, 2026-11-08). Each entry turns the matching
// check into an *expected* failure (Playwright `test.fail`): CI stays green while the defect is
// open and goes red the moment the UI is fixed, which is the cue to delete the entry. Set
// STORYBOOK_NO_BASELINE=1 to run every check for real and see the full list.

export type Check = 'overflow' | 'tap-targets' | 'axe';

export interface KnownIssue {
  /** Matched against the story id (for example `features-programs-programspage--no-programs`). */
  story: RegExp;
  check: Check;
  reason: string;
}

export const KNOWN_ISSUES: KnownIssue[] = [
  // Footer links Help / Privacy / Terms are 20 px tall on every dashboard story.
  {
    story: /^features-dashboard-dashboardpage--/,
    check: 'tap-targets',
    reason: 'footer links Help / Privacy / Terms are 30-47 × 20 px',
  },
  // Programs list cards: View Workout / Set Active / Delete are 40 px tall.
  {
    story: /^features-programs-programspage--(multiple-programs|single-active-program|all-completed)$/,
    check: 'tap-targets',
    reason: 'View Workout / Set Active / Delete buttons are 40 px tall',
  },
  // Exercise library: filter chips and view toggles.
  {
    story: /^features-exercises-exerciselibrarypage--/,
    check: 'tap-targets',
    reason: 'muscle-group chips are 26 px and equipment chips 24 px tall; Grouped / Grid / List toggles are 36 px',
  },
  // Setup wizard exercise step: equipment filter chips are 30 px, the "Add <exercise>" buttons 39 × 24.
  {
    story: /^features-workout-setupwizard--exercise-config-step$/,
    check: 'tap-targets',
    reason: 'equipment filter chips are 30 px tall and the Add buttons 39 × 24 px',
  },
  // Workout dashboard with a programme loaded: day / exercise action buttons under 44 px.
  {
    story: /^features-workout-workoutdashboard--(active-workout|deload-week|final-week|all-days-complete|with-minimal-sets-exercise)$/,
    check: 'tap-targets',
    reason: 'day and exercise action buttons are under 44 px',
  },
  // Setup wizard: the template list is a scrollable region without keyboard access (.gap-1).
  {
    story: /^features-workout-setupwizard--/,
    check: 'axe',
    reason: 'scrollable-region-focusable (serious): the template list (.gap-1) scrolls but cannot take focus',
  },
];

export function knownIssue(storyId: string, check: Check): KnownIssue | undefined {
  return KNOWN_ISSUES.find((issue) => issue.check === check && issue.story.test(storyId));
}
