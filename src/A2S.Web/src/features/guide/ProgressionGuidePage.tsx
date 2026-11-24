import { Navbar } from '@/components/layout/Navbar';
import { Card } from '@/components/ui/card';

/** Plain-English explanation of every progression scheme an exercise, cardio track or drill can use. */

interface Scheme {
  id: string;
  name: string;
  usedFor: string;
  summary: string;
  rules: string[];
  table?: { head: [string, string]; rows: [string, string][] };
}

const LIFTING: Scheme[] = [
  {
    id: 'linear',
    name: 'Linear (A2S2 / training max)',
    usedFor: 'Main lifts (T1) and secondary lifts (T2): squat, bench, deadlift, overhead press and their variations.',
    summary:
      'Every weight comes from your training max (TM), about 90 % of your one-rep max. Each week sets a percentage and a rep target; the last set is AMRAP (as many reps as possible), and how far you beat or miss the target moves the TM for next time.',
    rules: [
      'Working weight = TM × the week’s percentage, rounded to 2.5 kg (5 lb).',
      '5 working sets, the last one AMRAP. Deload weeks (7, 14, 21): 4 sets of 5 at 58 %, no AMRAP.',
      '21 weeks = 3 blocks of 7. Reps drop and intensity rises each block: T1 5 → 1 reps (70 → 96 %), T2 7 → 2 reps (70 → 92 %).',
      'AMRAP target: first half of a block = reps × 2, second half = reps × 2 − 1.',
      'If you have no TM yet, the sets are planned at 0 kg and you set the TM before finishing.',
    ],
    table: {
      head: ['AMRAP reps vs target', 'Training max change'],
      rows: [
        ['+5 or more', '+3 %'],
        ['+3 to +4', '+2 %'],
        ['+1 to +2', '+1 %'],
        ['Hit it exactly', 'No change'],
        ['1 short', '−2 %'],
        ['2 or more short', '−5 %'],
      ],
    },
  },
  {
    id: 'reps-per-set',
    name: 'Reps per set',
    usedFor: 'Accessories: machines, cables, dumbbells (e.g. 3 × 8–12).',
    summary:
      'Every set uses the same weight and aims for the top of a rep range. You earn extra sets first, then more weight.',
    rules: [
      'Success — every set reaches the top of the range: add a set. Already at your target sets: add weight and go back to the starting sets.',
      'Maintained — every set at least the bottom of the range: no change.',
      'Failed — any set below the bottom: remove a set, or drop the weight if you are already at the minimum sets.',
      'Weight steps: 2.5 kg for barbell, cable and machine; dumbbells 1 kg under 10 kg, then 2 kg.',
      'One-sided (unilateral) exercises cap at 3 sets per side.',
      'Cable and machine weight changes are confirmed from what you actually log next time, because stacks differ.',
      'A starting weight can be left blank and set after your first session.',
    ],
  },
  {
    id: 'minimal-sets',
    name: 'Minimal sets',
    usedFor: 'Bodyweight and assisted work: pull-ups, dips.',
    summary: 'Hit a total number of reps in as few sets as you can. The weight (or assistance) is yours to change.',
    rules: [
      'Success — total reached in fewer sets than now: next time uses one set fewer.',
      'Maintained — total reached in exactly the current sets: no change.',
      'Failed — total not reached: add a set.',
      'Sets stay between the minimum and maximum you choose.',
    ],
  },
];

const CARDIO: Scheme[] = [
  {
    id: 'week-program',
    name: 'Week program',
    usedFor: 'Plans laid out week by week, like walk → run (Couch to 5K style).',
    summary: 'A fixed list of weeks. You move to the next week once enough good sessions are done.',
    rules: [
      'Sessions needed per week = what your plan schedules for the track (never fewer than the stage’s minimum).',
      'Planned sessions missed since the week began count against the week.',
      'All of them good → next week. The last week finishes the stage.',
      'Any not good → it suggests repeating the week. Declining moves you on anyway.',
      'Two niggles in a week, or any “stop” pain flag → it suggests dropping back a week.',
    ],
  },
  {
    id: 'double-progression',
    name: 'Double progression',
    usedFor: 'Walk builder, stair finisher and other “more time, then harder” tracks.',
    summary:
      'First you add volume (minutes or interval rounds). At the top of the range the intensity (level, incline or speed) goes up and the volume resets lower.',
    rules: [
      'One step after every N good sessions (2 by default).',
      'Step size is fixed (e.g. +1 min) or a percentage, rounded up.',
      'At the top of volume: intensity +1 step, volume back to the reset value (or the bottom of the range).',
      'A completed session that wasn’t good holds you where you are.',
      'Two missed sessions in a row steps you back once.',
      'Top of both volume and intensity finishes the stage.',
    ],
  },
];

const TRACK_RULES = [
  'A track is a list of stages, each with its own scheme. Finishing one suggests the next — nothing moves until you accept.',
  'A good session: done as prescribed, no pain flag, and either not Brutal with heart rate at or under the zone ceiling (easy stages) or enough time in zones 4–5 (hard stages; Solid or Brutal effort counts when there is no heart rate).',
  'Warm-up and cool-down blocks never progress and are never judged.',
  'While a suggestion is waiting, or the track is paused or gated, sessions are saved but not judged.',
  'Benchmarks are tests every few weeks for your records. They never change the progression.',
  'Every change is logged with a reason and the last one can be undone.',
];

const MOBILITY: Scheme = {
  id: 'drill-levels',
  name: 'Drill levels',
  usedFor: 'Mobility drills that have levels.',
  summary: 'Each drill has a dose (reps or seconds) and optional harder levels.',
  rules: [
    '2 clean sessions in a row → the dose goes up one step within its range.',
    'At the top of the range → it suggests the next level, which you accept or decline.',
    'Strain or stop → steps back once.',
    'A phase change can add a few seconds to every timed hold.',
    'Every change is logged and the last one can be undone.',
  ],
};

function SchemeCard({ scheme }: { scheme: Scheme }) {
  return (
    <Card id={scheme.id} className="p-5 space-y-3 scroll-mt-24">
      <div>
        <h3 className="text-lg font-semibold">{scheme.name}</h3>
        <p className="text-caption mt-1">{scheme.usedFor}</p>
      </div>
      <p>{scheme.summary}</p>
      <ul className="list-disc pl-5 space-y-1 text-sm">
        {scheme.rules.map((r) => (
          <li key={r}>{r}</li>
        ))}
      </ul>
      {scheme.table && (
        <table className="w-full text-sm border-t border-border">
          <thead>
            <tr className="text-left text-muted-foreground">
              <th className="py-2 font-medium">{scheme.table.head[0]}</th>
              <th className="py-2 font-medium">{scheme.table.head[1]}</th>
            </tr>
          </thead>
          <tbody>
            {scheme.table.rows.map(([a, b]) => (
              <tr key={a} className="border-t border-border/50">
                <td className="py-1.5">{a}</td>
                <td className="py-1.5 tabular-nums">{b}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </Card>
  );
}

export function ProgressionGuidePage() {
  return (
    <div className="min-h-screen bg-background">
      <Navbar />
      <main className="container-page py-8 space-y-8 max-w-3xl">
        <div>
          <h1 className="text-hero">How progression works</h1>
          <p className="text-caption mt-2">
            Every scheme you can pick for an exercise, cardio track or drill, and exactly what moves it up or down.
          </p>
        </div>

        <section className="space-y-4">
          <h2 className="text-xl font-semibold">Weights</h2>
          {LIFTING.map((s) => (
            <SchemeCard key={s.id} scheme={s} />
          ))}
        </section>

        <section className="space-y-4">
          <h2 className="text-xl font-semibold">Cardio tracks</h2>
          <Card id="tracks" className="p-5 scroll-mt-24">
            <ul className="list-disc pl-5 space-y-1 text-sm">
              {TRACK_RULES.map((r) => (
                <li key={r}>{r}</li>
              ))}
            </ul>
          </Card>
          {CARDIO.map((s) => (
            <SchemeCard key={s.id} scheme={s} />
          ))}
        </section>

        <section className="space-y-4">
          <h2 className="text-xl font-semibold">Mobility</h2>
          <SchemeCard scheme={MOBILITY} />
        </section>
      </main>
    </div>
  );
}
