import { Button } from '@/components/ui/button';
import { formatWeekRange, isThisWeek, resolveWeek, shiftWeek } from './week';

interface WeekPickerProps {
  week: string;
  onChange: (week: string) => void;
}

/** Previous / next week with the Monday to Sunday range as the label, and a way back to this week. */
export function WeekPicker({ week, onChange }: WeekPickerProps) {
  return (
    <div className="flex items-center justify-between gap-2 rounded-lg border border-border bg-card p-2">
      <Button variant="ghost" size="icon" aria-label="Previous week" onClick={() => onChange(shiftWeek(week, -1))}>
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} aria-hidden="true">
          <path strokeLinecap="round" strokeLinejoin="round" d="M15 19l-7-7 7-7" />
        </svg>
      </Button>
      <div className="text-center">
        <div className="font-semibold text-foreground">{formatWeekRange(week)}</div>
        {isThisWeek(week) ? (
          <div className="text-caption">This week</div>
        ) : (
          <button
            type="button"
            className="inline-flex min-h-11 items-center px-3 text-sm text-primary underline-offset-4 hover:underline"
            onClick={() => onChange(resolveWeek(null))}
          >
            Back to this week
          </button>
        )}
      </div>
      <Button variant="ghost" size="icon" aria-label="Next week" onClick={() => onChange(shiftWeek(week, 1))}>
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} aria-hidden="true">
          <path strokeLinecap="round" strokeLinejoin="round" d="M9 5l7 7-7 7" />
        </svg>
      </Button>
    </div>
  );
}
