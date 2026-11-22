# Starter plan spec: "Cut, cardio & mobility — Nov 2026" (plan v3)

The data for a new one-tap suggested plan, alongside `SuggestedPlans/CutTo85Nov2026.cs`. Everything
here maps onto existing aggregates (A2S2 workout, training plan schedule, cardio tracks, mobility
routines). Weights are not set here: four training maxes are asked for at start, and every
accessory starts with a pending weight (`IsWeightPending`), confirmed after its first session.

## Plan

| Field | Value |
|---|---|
| Key | `cut-cardio-mobility-nov-2026` |
| Name | Cut, cardio & mobility — Nov 2026 |
| Description | 4 focused ~60 min sessions (A2S2 four-day), conditioning finishers on upper days, zone 2 walking, three home mobility/yoga routines. 6 months: lose weight, build cardio, restore mobility; squat pattern first. |
| Start | Coming Monday |
| Variant | `ProgramVariant.FourDay` |
| Phases | Cut from start to 14 Mar 2027, then Maintenance to 23 May 2027 |
| Checkpoints, habits, weekly allowances, high-intensity target | Same as Cut to 85 (checkpoints to 85 kg by 8 Mar 2027) |

### Inputs asked at start

| Key | Label | Unit |
|---|---|---|
| `benchTm` | Bench press training max | kg |
| `pauseSquatTm` | Pause squat training max | kg |
| `inclineSmithTm` | Incline Smith press training max | kg |
| `frontSquatTm` | Front squat training max | kg |
| `startWeightKg` | Start weight | kg |

## Lifting days

Order is the session order. `2a/2b` and `3a/3b` are supersets (`SupersetSpec`). Main lifts are
`ExerciseCategory.MainLift` with `LinearProgressionStrategy`. Accessories are `RepsPerSetStrategy`.
"Left +1" means the extra set on the left side; until `Exercise` has a side field, put it in
`ExerciseNotes`, as Cut to 85 does.

### Day 1 — Upper A (Mon)

| # | Hevy template id | Exercise | Category / tier | Sets | Reps | Per side | Notes |
|---|---|---|---|---|---|---|---|
| 1 | `79D0BB3A` | Bench Press (Barbell) | MainLift, T1 Primary | A2S2 | A2S2 | | TM input `benchTm` |
| 2a | `55E6546F` | Bent Over Row (Barbell) | Accessory | 3 | 8–10 | | Brace hard, fixed torso angle, no jerking |
| 2b | `DE68C825` | Single Arm Lateral Raise (Cable) | Accessory | 2 | 12–15 | yes | |
| 3a | `D0C4A899` | Single Arm Cable Row | Accessory | 2 | 10–12 | yes | |
| 3b | `68F8A292` | Skullcrusher (Dumbbell) | Accessory | 2 | 10–12 | | |
| 4 | `CC55119B` | Cable Core Palloff Press | Accessory | 2 | 10 | yes | |

Finisher: Conditioning track (below).

### Day 2 — Lower A (Tue)

| # | Hevy template id | Exercise | Category / tier | Sets | Reps | Per side | Notes |
|---|---|---|---|---|---|---|---|
| 1 | `CE1054CE` | Pause Squat (Barbell) | MainLift, T1 Primary | A2S2 | A2S2 | | TM input `pauseSquatTm`. Becomes Squat (Barbell) in phase 3 |
| 2 | `72CFFAD5` | Romanian Deadlift (Dumbbell) | Accessory | 3 | 8–10 | | The week's only hinge |
| 3a | `20C1A3CB` | Split Squat (Dumbbell) | Accessory | 2 | 8–10 | yes | Left +1 |
| 3b | `F4B4C6EE` | Hip Abduction (Machine) | Accessory | 2 | 12–15 | | |

No finisher. Session note: "Cool-down: 10 min easy incline walk."

### Day 3 — Upper B (Thu)

| # | Hevy template id | Exercise | Category / tier | Sets | Reps | Per side | Notes |
|---|---|---|---|---|---|---|---|
| 1 | `3A6FA3D1` | Incline Bench Press (Smith Machine) | MainLift, T2 Auxiliary | A2S2 | A2S2 | | TM input `inclineSmithTm` |
| 2a | `cd39c85d-7449-43b7-8722-417f5fc97e8b` | Lat Pulldown Neutral Grip | Accessory | 3 | 8–12 | | |
| 2b | `BE640BA0` | Face Pull | Accessory | 2 | 15 | | |
| 3a | `234897AB` | Rope Cable Curl | Accessory | 2 | 10–15 | | |
| 3b | `93A552C6` | Triceps Pushdown | Accessory | 2 | 12–15 | | |
| 4 | `92D858EA` | Cable Twist (Down to up) | Accessory | 2 | 10 | yes | Left +1 (turning left). Turn from the ribs, hips face forward |

Finisher: Conditioning track (below).

### Day 4 — Lower B (Fri)

| # | Hevy template id | Exercise | Category / tier | Sets | Reps | Per side | Notes |
|---|---|---|---|---|---|---|---|
| 1 | `5046D0A9` | Front Squat | MainLift, T2 Auxiliary | A2S2 | A2S2 | | TM input `frontSquatTm` |
| 2 | `C7973E0E` | Leg Press (Machine) | Accessory | 3 | 10–12 | | |
| 3a | `68CE0B9B` | Hip Thrust (Machine) | Accessory | 2 | 10–12 | | Plate-loaded |
| 3b | `8BEBFED6` | Hip Adduction (Machine) | Accessory | 2 | 12–15 | | |
| 4 | `11A123F3` | Seated Leg Curl (Machine) | Accessory | 2 | 10–12 | | |

No finisher. Session note: "Cool-down: 10 min easy incline walk."

## Weekly schedule (`ScheduledSession` slots)

| Day | Kind | Lift day | Warm-up routine | Finisher | Cardio track | Mobility routine | Label |
|---|---|---|---|---|---|---|---|
| Mon | Lift | Day1 | Warm-up (upper) | Conditioning | | | Upper A |
| Mon | Mobility | | | | | Hips & lower back | Hips & lower back (15 min) |
| Tue | Lift | Day2 | Warm-up (lower) | | | | Lower A |
| Tue | Mobility | | | | | Upper back & chest | Upper back & chest (15 min) |
| Wed | Cardio | | | | Walk builder | | Walk builder (zone 2) |
| Wed | Mobility | | | | | Full-body flow | Full-body flow (20 min) |
| Thu | Lift | Day3 | Warm-up (upper) | Conditioning | | | Upper B |
| Thu | Mobility | | | | | Hips & lower back | Hips & lower back (15 min) |
| Fri | Lift | Day4 | Warm-up (lower) | | | | Lower B |
| Fri | Mobility | | | | | Upper back & chest | Upper back & chest (15 min) |
| Sat | Cardio | | | | Long walk | | Long walk outdoors |
| Sat | Mobility | | | | | Full-body flow | Full-body flow or a Man Flow video |
| Sun | Rest | | | | | | Rest: daily steps only |

`PlanScheduleRules` should raise no warnings for this layout: no hard cardio the day before a lower day,
because the finishers sit on upper days and come after lifting.

## Cardio tracks (created with the plan)

| Track | From template | Changes to the template | Used by |
|---|---|---|---|
| Conditioning | `stair-finisher` | Rename to "Conditioning". Unchanged stages (continuous 5→10 min at a level, then 1:1 intervals 4→8 rounds). Skip in A2S2 deload weeks once that exists. | Finisher on Upper A and Upper B |
| Walk builder | `walk-builder` | None (20→45 min, then incline +1 % to 10 %) | Wed |
| Long walk | `walk-builder` | Rename to "Long walk", note "Outdoors". Single stage: Double progression, minutes 45→75, step 10, no intensity, 2 sessions to advance, Easy, HR ceiling zone 2 | Sat |
| Walk → run | `walk-to-run` | **Not created.** Start it later, once the running gate passes: trend weight ≤ 90 kg, walk builder at 45 min with ≥ 8 % incline and rated Easy for 2 weeks, no knee/shin/Achilles niggles for 4 weeks | — |

Norwegian 4×4 is not scheduled.

## Mobility routines (created with the plan)

Each drill: `name, sets, dose, perSide, extraSetSide, cues, standingAlternative`. Until drills have
progression levels, the **cues field carries the cues plus "Easier: … Harder: …"**, so the player shows
them.

### Warm-up (upper), 8 min

| Drill | Sets | Dose | Per side | Extra set | Cues |
|---|---|---|---|---|---|
| 90/90 breathing, feet on a bench | 1 | 5 breaths | | | Exhale fully until the ribs drop. Tongue on the roof of the mouth, jaw loose. |
| Open books | 1 | 5 reps | yes | Left | Knees stacked and pinned. Rotate from the upper back, not the lower. |
| Wall slide with lift-off | 1 | 8 reps | | | Lower back stays on the wall. Lift the hands off at the top. |
| Band pull-apart | 1 | 15 reps | | | Arms straight. Shoulders down, away from the ears. |

Standing alternatives: 90/90 breathing → "Back-to-wall breathing, ribs down"; open books → "Side-on wall rotation".

### Warm-up (lower), 8 min

| Drill | Sets | Dose | Per side | Extra set | Cues |
|---|---|---|---|---|---|
| 90/90 breathing, feet on a bench | 1 | 5 breaths | | | Exhale fully until the ribs drop. Jaw loose. |
| Quadruped hip shift | 1 | 5 breaths | | | Sit the hips back and left without the lower back rounding. |
| Glute bridge + mini-band walk | 1 | 10 reps | | | 10 bridges, then 10 steps each way. Ribs down. Feel it in the side of the hip. |
| Bodyweight pause squat, 3 s hold | 1 | 5 reps | | | Spread the floor with your feet. Same depth and hip path every rep. |

### Hips & lower back, 15 min

| Drill | Sets | Dose | Per side | Extra set | Cues (with Easier / Harder) |
|---|---|---|---|---|---|
| 90/90 breathing, feet on a wall | 1 | 6 breaths | | | Flatten the lower back gently. Exhale until the ribs drop. Easier: feet on the floor, knees bent. Harder: squeeze a ball or foam roller between the knees. |
| Half-kneeling hip flexor stretch | 2 | 30 s | yes | Left | Tuck the tail under first. Squeeze the back glute, then shift forward a few cm. Easier: cushion under the knee, smaller shift. Harder: reach the same-side arm up and lean slightly away. |
| 90/90 hip switches | 1 | 6 reps | yes | | Sit tall. Rotate from the hips, not the lower back. Easier: hands supporting, small range. Harder: hands off, lift the back knee at each end. |
| Side-lying clamshell | 2 | 12 reps | yes | Left | Heels together, hips stacked. Open the knee without rolling back. Easier: smaller range. Harder: mini band, or from a side plank on the knees. |
| Child's pose with side reach | 1 | 30 s | yes | Left | Walk the hands to one side. Hips back. Breathe into the opposite ribs. Easier: cushion under the hips. Harder: cross the far wrist over the near one. |
| Figure-4 stretch on your back | 1 | 30 s | yes | | Ankle on the opposite knee. Draw the thigh in. Tailbone down. Easier: foot against a wall. Harder: seated, tall spine. |
| Side plank | 2 | 25 s | yes | Left | Elbow under the shoulder. Hips forward in one straight line. Easier: knees down. Harder: lift the top leg. |
| Copenhagen plank, short lever | 2 | 15 s | yes | | Top knee on the sofa edge. Squeeze it down to lift the hips. Easier: bottom foot lightly touching. Harder: ankle on the edge (long lever). |

### Upper back & chest, 15 min

| Drill | Sets | Dose | Per side | Extra set | Cues (with Easier / Harder) |
|---|---|---|---|---|---|
| Cat-cow | 1 | 8 reps | | | One vertebra at a time. Exhale to round, inhale to lift the chest. Easier: smaller range. Harder: hold each end for 2 breaths. |
| Thread the needle | 1 | 5 reps | yes | | Hips over knees. Rest the shoulder down, look under the arm. Hold the last rep 20 s. Easier: stay on the forearm. Harder: top arm reaches up and over the head. |
| Open book | 1 | 5 reps | yes | Left | Knees pinned. Let the top arm open toward the floor, exhale at the end. Easier: top hand on the chest. Harder: 3 s pause at the end. |
| Doorway chest stretch | 2 | 30 s | yes | | Set 1 elbow at shoulder height, set 2 higher. Step through gently; shoulder down and back. Easier: smaller step. Harder: rotate the chest away. |
| Puppy pose | 1 | 6 breaths | | | Hips over knees, walk the hands forward, melt the chest down, ribs in. Easier: forearms down. Harder: hands on a block, chest lower. |
| Reverse plank (ladder) | 3 | 15 s | | | Hands under the shoulders. Press the floor away, hips level, chest open. Level 1: knees bent (tabletop). Level 2: hands on a bench, legs straight. Level 3: floor. +5 s per set after 2 clean sessions up to 30 s, then the next level at 15 s. |
| Dead bug | 2 | 8 reps | yes | | Lower back gently pressed down. Exhale as you reach. Stop before the back lifts. Easier: legs only. Harder: hold a light weight. |

### Full-body flow, 20 min

| Drill | Sets | Dose | Per side | Extra set | Cues (with Easier / Harder) |
|---|---|---|---|---|---|
| Warm-up flow: reach up, fold, half lift, plank, knees down, cobra, downward dog | 5 | 7 breaths | | | One breath per move, soft knees in the fold, push the hips back in downward dog. Easier: hands on a chair, skip the plank. Harder: low plank; hold downward dog for 5 breaths. |
| Low lunge with overhead reach | 1 | 5 breaths | yes | Left | Back knee down, tail tucked. Reach up and slightly away, ribs down. Easier: hands on the thigh. Harder: lift the back knee. |
| Warrior 2 to reverse warrior | 1 | 10 breaths | yes | | Front knee over the middle toes, hips open. 5 breaths each. Easier: shorter stance. Harder: deeper knee, longer stance. |
| Lizard | 1 | 5 breaths | yes | | Front foot outside the hand, back knee down, hips forward and down. Easier: forearms on the sofa. Harder: forearms to the floor. |
| Bridge | 3 | 5 breaths | | | Tilt the pelvis, peel up, push through the heels, no arch at the top. Easier: smaller lift. Harder: single leg, extra set left. |
| Supine twist, then rest | 1 | 30 s | yes | | Knees together, both shoulders heavy, jaw loose. Then 2 min lying still. Easier: cushion under the knees. Harder: straight top leg. |

A Man Flow video can stand in for the Saturday flow. Until follow-along routines exist, log it as
Yoga in Hevy (or mark the flow done).

## What the app still needs for this plan (separate PRs)

1. **Drill progression levels** (brief § 5c): replace the "Easier / Harder" text in cues with ordered
   levels per drill and a suggestion to move up after 2 clean sessions.
2. **Deload awareness for finishers**: skip the Conditioning finisher in A2S2 deload weeks.
3. **Follow-along routines**: title, link and minutes, for Man Flow videos.
4. **Cardio scheme/machine separation** (the cardio refactor prompt), so "Conditioning" can run on any machine.
