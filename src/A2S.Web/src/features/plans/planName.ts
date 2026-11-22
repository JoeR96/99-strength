/** Plan name rules, matching TrainingPlan.Rename on the server. */

export const MAX_PLAN_NAME_LENGTH = 200;

/** Null when the name is fine, else what is wrong with it. */
export function planNameError(name: string): string | null {
  const trimmed = name.trim();
  if (trimmed === '') return 'Give the plan a name.';
  if (trimmed.length > MAX_PLAN_NAME_LENGTH) return `Keep it to ${MAX_PLAN_NAME_LENGTH} characters or fewer.`;
  return null;
}
