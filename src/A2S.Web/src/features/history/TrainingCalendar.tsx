import { useRef, useState } from 'react';
import { Card } from '@/components/ui/card';
import { cn } from '@/lib/utils';
import { blockColors, getBlockColor } from '@/lib/blockColors';
import { MONTHS, type CalendarDay, type CalendarGrid } from './calendarData';
import { sessionTotals, topSet } from './historyStats';
import type { ExerciseHistoryDto, WorkoutActivityDto } from './historyTypes';

const WEEKDAYS = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
const TOOLTIP_WIDTH = 256; // w-64

const shortDate = (date: Date) =>
  date.toLocaleDateString(undefined, { weekday: 'short', day: 'numeric', month: 'short' });

const kg = (value: number) => Math.round(value).toLocaleString();

interface Hover {
  day: CalendarDay;
  left: number;
  top: number;
  /** Shown under the cell when there is no room above it */
  below: boolean;
}

interface TrainingCalendarProps {
  grid: CalendarGrid;
  /** For exercise names in the tooltip */
  exercises: ExerciseHistoryDto[];
  selectedDate?: Date;
  onSelect?: (activity: WorkoutActivityDto, date: Date) => void;
}

/**
 * The whole program as one grid of days, coloured by training block.
 *
 * From `md` up the weeks run left to right (a column each, Monday at the top), so 21 weeks
 * fit across one card. On a phone the same cells flow as an ordinary calendar, a week per
 * row, which keeps them big enough to tap.
 */
export function TrainingCalendar({ grid, exercises, selectedDate, onSelect }: TrainingCalendarProps) {
  const wrapRef = useRef<HTMLDivElement>(null);
  const [hover, setHover] = useState<Hover | null>(null);

  const showTooltip = (day: CalendarDay, cell: HTMLElement) => {
    const wrap = wrapRef.current;
    if (!wrap) return;
    const outer = wrap.getBoundingClientRect();
    const rect = cell.getBoundingClientRect();
    const centre = rect.left - outer.left + rect.width / 2;
    const half = TOOLTIP_WIDTH / 2;
    const below = rect.top - outer.top < 130;
    setHover({
      day,
      // Keep the whole tooltip inside the card
      left: Math.min(Math.max(centre, half), Math.max(half, outer.width - half)),
      top: below ? rect.bottom - outer.top + 8 : rect.top - outer.top - 8,
      below,
    });
  };

  const sessions = grid.days.filter((d) => d.activity).length;
  // The phone layout stops at the current week: ten rows of blank days are only scrolling
  const todayIndex = grid.days.findIndex((d) => d.isToday);
  const lastPhoneDay = todayIndex < 0 ? grid.days.length : Math.floor(todayIndex / 7) * 7 + 6;

  return (
    <Card className="p-6">
      <div className="mb-4">
        <h2 className="text-xl font-semibold text-foreground">Training calendar</h2>
        <p className="text-caption mt-1">
          {sessions} sessions so far in a {grid.weekCount}-week program. Pick a day to see its sets.
        </p>
      </div>

      <div ref={wrapRef} className="relative">
        {/* Month names over the week columns (wide layout only) */}
        <div className="mb-1 hidden gap-1 md:flex" aria-hidden="true">
          <div className="w-9 shrink-0" />
          <div
            className="grid flex-1 gap-1 text-xs text-muted-foreground"
            style={{ gridTemplateColumns: `repeat(${grid.weekCount}, minmax(0, 1fr))` }}
          >
            {grid.months.map((month) => (
              <span key={month.week} style={{ gridColumnStart: month.week + 1 }}>
                {month.label}
              </span>
            ))}
          </div>
        </div>

        {/* Weekday names along the top (phone layout only) */}
        <div className="mb-1 grid grid-cols-7 gap-1 text-center text-xs text-muted-foreground md:hidden" aria-hidden="true">
          {WEEKDAYS.map((name) => (
            <span key={name}>{name}</span>
          ))}
        </div>

        <div className="md:flex md:gap-1">
          <div className="hidden w-9 shrink-0 grid-rows-7 gap-1 text-xs text-muted-foreground md:grid" aria-hidden="true">
            {WEEKDAYS.map((name) => (
              <span key={name} className="flex items-center">
                {name}
              </span>
            ))}
          </div>

          <div className="grid flex-1 grid-cols-7 gap-1 md:auto-cols-fr md:grid-flow-col md:grid-cols-none md:grid-rows-7">
            {grid.days.map((day, index) => (
              <DayCell
                key={day.date.getTime()}
                day={day}
                wideOnly={index > lastPhoneDay}
                selected={!!selectedDate && day.date.toDateString() === selectedDate.toDateString()}
                onSelect={onSelect}
                onShow={showTooltip}
                onHide={() => setHover(null)}
              />
            ))}
          </div>
        </div>

        {hover?.day.activity && (
          <SessionTooltip hover={hover} activity={hover.day.activity} exercises={exercises} />
        )}
      </div>

      <Legend />
    </Card>
  );
}

const CELL = 'flex aspect-square items-center justify-center rounded-md text-xs font-medium tabular-nums';

function DayCell({
  day,
  wideOnly,
  selected,
  onSelect,
  onShow,
  onHide,
}: {
  day: CalendarDay;
  /** Hidden in the phone layout */
  wideOnly: boolean;
  selected: boolean;
  onSelect?: (activity: WorkoutActivityDto, date: Date) => void;
  onShow: (day: CalendarDay, cell: HTMLElement) => void;
  onHide: () => void;
}) {
  const { date, activity } = day;
  // On a phone the 1st of a month carries the month's name, since there is no row of month labels
  const label =
    date.getDate() === 1 ? (
      <>
        <span className="md:hidden">{MONTHS[date.getMonth()]}</span>
        <span className="hidden md:inline">1</span>
      </>
    ) : (
      date.getDate()
    );

  if (wideOnly) return <div className={cn(CELL, 'hidden bg-muted/20 md:flex')} aria-hidden="true" />;
  if (day.beforeStart) return <div className={CELL} aria-hidden="true" />;

  if (!activity) {
    return (
      <div
        className={cn(
          CELL,
          day.isFuture ? 'bg-muted/20' : 'bg-muted/40 text-muted-foreground',
          day.isToday && 'text-foreground ring-2 ring-primary'
        )}
        aria-hidden="true"
      >
        {/* Days still to come stay blank: a number there would be noise, and too faint to read */}
        {!day.isFuture && label}
      </div>
    );
  }

  const colour = getBlockColor(activity.blockNumber);
  const totals = sessionTotals(activity);
  return (
    <button
      type="button"
      onClick={() => onSelect?.(activity, date)}
      onMouseEnter={(e) => onShow(day, e.currentTarget)}
      onMouseLeave={onHide}
      onFocus={(e) => onShow(day, e.currentTarget)}
      onBlur={onHide}
      aria-pressed={selected}
      aria-label={`${shortDate(date)}: week ${activity.weekNumber}, day ${activity.dayNumber}${
        activity.isDeloadWeek ? ', deload' : ''
      }. ${totals.exercises} exercises, ${totals.sets} sets.`}
      className={cn(
        CELL,
        'cursor-pointer transition-shadow hover:ring-2 hover:ring-foreground/60',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
        activity.isDeloadWeek ? 'border-2' : 'text-background',
        selected && 'ring-2 ring-foreground ring-offset-2 ring-offset-card'
      )}
      // A deload session is an outline in the block's colour: lighter work, lighter mark
      style={
        activity.isDeloadWeek
          ? { borderColor: colour, color: colour, backgroundColor: `color-mix(in srgb, ${colour} 16%, transparent)` }
          : { backgroundColor: colour }
      }
    >
      {label}
    </button>
  );
}

function SessionTooltip({
  hover,
  activity,
  exercises,
}: {
  hover: Hover;
  activity: WorkoutActivityDto;
  exercises: ExerciseHistoryDto[];
}) {
  const totals = sessionTotals(activity);
  const best = topSet(activity);
  const bestName = best && exercises.find((e) => e.exerciseId === best.exerciseId)?.name;

  return (
    <div
      // The cell's aria-label already says all of this
      aria-hidden="true"
      className="pointer-events-none absolute z-10 w-64 rounded-md border border-border bg-popover p-3 text-sm text-popover-foreground shadow-lg"
      style={{
        left: hover.left,
        top: hover.top,
        transform: `translate(-50%, ${hover.below ? '0' : '-100%'})`,
      }}
    >
      <div className="flex items-center justify-between gap-2">
        <span className="font-semibold">{shortDate(hover.day.date)}</span>
        <span className="flex items-center gap-1.5 text-xs text-muted-foreground">
          <span className="h-2 w-2 rounded-full" style={{ backgroundColor: getBlockColor(activity.blockNumber) }} />
          Week {activity.weekNumber} · Day {activity.dayNumber}
        </span>
      </div>
      <p className="mt-1.5 text-xs text-muted-foreground">
        {totals.exercises} exercises · {totals.sets} sets · {kg(totals.volume)} kg
        {activity.isDeloadWeek && ' · deload'}
      </p>
      {best && bestName && (
        <p className="mt-2 border-t border-border/50 pt-2 text-xs">
          <span className="text-muted-foreground">Top set</span>{' '}
          <span className="font-medium">{bestName}</span>
          <br />
          <span className="font-mono">
            {best.weight} {best.unit} × {best.reps}
          </span>
          {best.wasAmrap && (
            <span className="text-muted-foreground"> · est. 1RM {best.estimate.toFixed(1)} {best.unit}</span>
          )}
        </p>
      )}
    </div>
  );
}

function Legend() {
  const swatch = 'h-3 w-3 rounded-sm';
  return (
    <div className="mt-4 flex flex-wrap gap-x-4 gap-y-2 text-xs text-muted-foreground">
      {Object.keys(blockColors)
        .map(Number)
        .map((block) => (
          <span key={block} className="flex items-center gap-1.5">
            <span className={swatch} style={{ backgroundColor: getBlockColor(block) }} />
            Block {block}
          </span>
        ))}
      <span className="flex items-center gap-1.5">
        <span className={cn(swatch, 'border-2 border-muted-foreground')} />
        Deload
      </span>
      <span className="flex items-center gap-1.5">
        <span className={cn(swatch, 'bg-muted/40')} />
        Rest day
      </span>
      <span className="flex items-center gap-1.5">
        <span className={cn(swatch, 'ring-2 ring-primary')} />
        Today
      </span>
    </div>
  );
}
