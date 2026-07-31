# Background Planning for Colony Manager Redux

Companion to [Work Tab for Colony Manager Redux](https://github.com/archdukejim/rimworld-work-manager-plugin).
Turns colony management into paperwork.

## What changes

**The thinking moves off the colonist.** Colony Manager Redux normally works a manager job out while
a colonist stands at the desk. Here the gather phase — which reads the map and changes nothing —
runs on its own, and what it decides is parked until someone enacts it.

**Schedules are anchored to a time of day.** Instead of "four hours after the last run, whenever
that was", a four-hourly job is planned at 4am, 8am, noon, 4pm, 8pm and midnight, every day. A
daily job is planned once, at 4am. The anchor hour is configurable — early morning keeps the
thinking out of the colony's working day.

**Planning runs flat out.** Colony Manager Redux paces its work thinly across many ticks to stay
out of the way of the running game. Background planning happens while the colony sleeps and nothing
is waiting on it, so it's allowed to burst: the pacing is multiplied for the duration of a
background run only (20x by default) and released as soon as it finishes.

**The result is a bill.** Each plan is posted to the manager desk as an ordinary production bill,
and shows up in the desk's Bills tab. A manager picks it up under the Managing work type and works
through it. Only one plan waits at a time — an unclaimed plan is superseded when the next scheduled
one is drawn up.

**The plan is an unfinished item.** While it's being worked it exists as a book on the desk. It
can't be hauled, costs nothing to start, returns nothing when lost, holds its progress if the
manager is called away, and vanishes when it's signed off. Discarding the draft throws away the
write-up but leaves the bill, so the colony doesn't have to think it all through again.

**Managers are paid in both skills.** Managing speed already scales off Intellectual and Social in
Colony Manager Redux. The work grants Intellectual experience as it goes and Social on completion
(half the rate by default, configurable).

**Enacting is immediate.** The moment the bill completes, every designation the plan decided on is
applied to the map at once.

## Settings

| Setting | Default | What it does |
| --- | --- | --- |
| Plan at | 4am | The hour every planning schedule is anchored to. |
| Replace at-the-desk planning | on | Stops Colony Manager Redux sending colonists to the desk to plan from scratch. Turning this off runs both systems. |
| Require a powered station | on | Only plan when there's a usable, powered manager station. |
| Background planning speed | 20x | How much faster planning runs when nobody is waiting on it. Lower it if you see a stutter at the planning hour. |
| Social experience | 50% | Social experience granted on completion, as a share of the Intellectual experience the work grants. |

## Building

```bash
dotnet build Source/ManagerBackgroundPlanning.csproj
```

Output goes to `1.6/Assemblies/`. Colony Manager Redux, ilyvion's Laboratory and Harmony are
referenced from the Steam workshop install; override `-p:WorkshopPath=...` if yours is elsewhere.
Harmony is referenced for compilation only and is never copied into the output.

## Requirements

- RimWorld 1.6
- Harmony
- Colony Manager Redux (and ilyvion's Laboratory)

## Notes

The book texture is a placeholder drawn for this mod; replace
`Common/Textures/Things/Item/Unfinished/WMC_ManagementPlan.png` with better art at any time.

The AI manager building is left alone — it does the colony's managing by itself, which is the point
of it, and this mod doesn't interfere with that path.
