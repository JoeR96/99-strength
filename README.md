# 99 Strength

A phone-first training planner. It runs your whole week from one screen: lifting on Greg Nuckols'
Average to Savage 2 (A2S2) programme, cardio, mobility, daily habits and body-weight checkpoints.
You log your lifts in Hevy, and 99 Strength works out what comes next.

Live at **https://99-strength.enjoeneer.dev**. Install it to your home screen (steps below).

## What it does

**Today** shows what's on today and nothing else: the session (lift, cardio, mobility or rest) with a
big Start button, habit counters you tap once to log, this morning's weigh-in, weekly goal rings and your streak.

**Plan** is the weekly schedule plus phases (cut, maintenance, pause, deload) and weight checkpoints.
Pausing for a holiday freezes the programme, and missed sessions during a pause aren't counted as failures.

**Lifting** follows A2S2: a 21-week cycle of three 7-week blocks, each ending in a deload. The last set of
a main lift is done for as many reps as possible (AMRAP), and your result there moves the training max up or down.
Accessories progress on their own rules: add a set, add weight, or drop back when you miss.

**Cardio** has three protocols built in: Norwegian 4×4 intervals, Couch to 5K (with "repeat this week"),
and steady easy work. Each session logs its duration, effort, and minutes in heart-rate zones 4–5. The zone minutes count toward a weekly target.

**Mobility** routines are ordered lists of drills, each with sets, reps, seconds or breaths. A drill can
add an extra set for one side and list a standing alternative for when you can't get on the floor.

**Habits** come in three types: counters (water 3/6), numbers (weight) and checkboxes. Each habit has a minimum
for bad days, and the streak uses a "never miss twice" rule: one missed day keeps it, two in a row breaks it.

**Progress** shows your weight trend against the checkpoints, plus hard sets per muscle group this
week against your caps.

**Meals and shopping list** let you pick the week's meals and get a combined shopping list to share or copy.

## How it connects

| Integration | Direction | What flows |
|---|---|---|
| **Hevy** | both ways | Each scheduled session is pushed to Hevy as a routine, with warm-up drills first. Finished Hevy workouts are pulled back and matched to the planned session, and AMRAP results drive the A2S2 progression as if you'd entered them by hand. A workout that matches nothing goes to an inbox, where one tap assigns it. Weigh-ins are mirrored to Hevy. Needs a Hevy Pro API key, which the server stores encrypted. The browser never sees it. |
| **Push notifications** | server → phone | "Time to train" at the session's start time, and an evening nudge when a habit is still under its minimum. Uses standard Web Push. On iPhone it only works from the installed home-screen app (iOS 16.4+). |
| **Desktop widgets and scripts** | both ways, limited | Personal API tokens (`nns_pat_…`, created in Settings) can read and log habits and weigh-ins and read today's session, and nothing else. The Terran CC habit widget uses one. |
| **Sign-in** | — | Clerk. |
| **Offline** | — | The app shell is cached. Habit taps made without signal are queued on the phone and synced later, each one dated to the day it was made. |

Not connected, by design: MacroFactor (it has no usable API) and calorie or protein tracking.

## Install on your phone

**iPhone (Safari, iOS 16.4+)**: open the site, tap **Share → Add to Home Screen → Add**, then launch it from
the home screen. Notifications only work from there, and only after you turn them on in Settings.

**Android (Chrome)**: open the site, tap **⋮ → Install app**.

## How it's built

A .NET 10 API (Clean Architecture, CQRS, PostgreSQL) and a React 19 PWA. Its seams:

- **Domain**: the A2S2 workout and its progression strategies, the training plan, cardio protocols, mobility
  routines, habits, body weight and meals. No framework dependencies.
- **Hevy anti-corruption layer**: every call to Hevy goes through the server, with retries and rate-limit handling.
- **Push sender**: a background job that sends each nudge at most once per person per local day.
- **Local time**: all dates are Europe/London local dates, so streaks and week rollover stay correct across clock changes.

Developer and agent documentation: [`AGENTS.md`](AGENTS.md) for architecture and conventions, and
[`docs/development.md`](docs/development.md) for running, testing, secrets and deploying.
