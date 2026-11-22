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
  // Empty since 2026-11-22: every page story passes overflow, 44 px tap targets and axe (serious/critical).
  // Add an entry only for a defect you are deliberately leaving open, with the reason.
];

export function knownIssue(storyId: string, check: Check): KnownIssue | undefined {
  return KNOWN_ISSUES.find((issue) => issue.check === check && issue.story.test(storyId));
}
