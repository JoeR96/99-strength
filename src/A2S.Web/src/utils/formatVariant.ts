/**
 * The API serializes variant as the C# enum member name (e.g. "FourDay"), not a
 * number, so concatenating it directly produced "FourDay-Day Split". Map known
 * variant names to their day count for display; fall back to the raw value.
 * Handles both string (from WorkoutSummaryDto) and number (from WorkoutDto).
 */
const VARIANT_DAY_LABEL: Record<string, string> = {
  FourDay: '4',
  FiveDay: '5',
  SixDay: '6',
};

const VARIANT_NUMBER_LABEL: Record<number, string> = {
  4: '4',
  5: '5',
  6: '6',
};

export function formatVariantDays(variant: string | number): string {
  if (typeof variant === 'number') {
    return VARIANT_NUMBER_LABEL[variant] ?? String(variant);
  }
  return VARIANT_DAY_LABEL[variant] ?? variant;
}
