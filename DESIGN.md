# Design notes

Where the mod is, and what it's agreed to become. Everything under "Agreed, not built" was settled
in discussion but isn't in the code yet.

## Built

Background planning at anchored slots, posted to the manager desk as a bill, worked as an unfinished
book, applied to the map on completion. Free anchor hour (0–23), interval inherited from each
manager job. Coroutine pacing boosted during background runs only. Harmony patch suppressing Colony
Manager Redux's at-the-desk planning.

## Agreed, not built

### Cadence — tighten to fixed slots

Drop the free anchor hour and stop inheriting each job's `UpdateInterval`. One setting with two
values: **Daily (4am)**, default, and **Morning and afternoon (4am, 4pm)**.

Per-job update intervals stop driving background planning; the cadence does. Colony Manager Redux's
job list still shows those intervals, so they become misleading and should be noted or greyed out.

Why: intervals that don't divide 24 produce uneven slots and are hard to reason about — the current
settings preview and the scheduler actually disagree for those cases. Expiry also stops being a
judgement call: it's simply the next slot, instead of the shortest managed interval.

### One bill per manager job row, all posted together

`TryGatherNextJobWork` gathers `NextJob` — a single job. The current "post one, supersede the rest"
rule therefore plans only one job per slot and silently starves the others. Fix: at each slot, loop
the gather until it returns null and post every due plan as its own bill.

This supersedes an earlier idea of batching a cycle's plans into one bill; that can't work once
plans belong to different work types.

Clear-area and chop-wood are already separate job rows in Colony Manager Redux, so "each its own
bill" follows from this with no extra machinery.

### Planning as a sub-job of each work type

A `WorkGiverDef` is a sub-job within a work type, so one per domain: "plan hunting" under Hunting,
"plan forestry" under PlantCutting, and so on, plus the existing one under Managing which accepts
everything. A bill carries its job's `ManagerJob.WorkTypeDef`; each domain work giver accepts only
matching bills.

A hunter with Managing disabled can still plan hunts. A dedicated manager can still do all of them.
Jobs with a null `WorkTypeDef` — Colony Manager Redux's Power tab, the companion mod's Labor tab —
fall back to Managing, which is correct: those genuinely are administrative.

Write defs by hand for the vanilla domains (PlantCutting, Mining, Hunting, Handling, Crafting) plus
the Managing fallback, rather than generating them per `WorkTypeDef` at startup. Modded manager tabs
degrade to the fallback.

### Domain skill on top of intelligence

One stat per domain, combining sources the same way `ManagingSpeed` itself does:

```
WMC_PlanningSpeed_<domain>
  statFactors:       ManagingSpeed      # already carries Intellectual + Social
  skillNeedFactors:  <domain skill>     # base 0.6, +0.04/level -> 0.6x at 0, 1.4x at 20
```

The domain skill becomes the recipe's `workSkill` and earns during the work; Intellectual and Social
are paid on completion. Modest span on purpose: a brilliant generalist and a grizzled expert both
plan well, only the pawn who is neither is slow.

### Work amount per bill, not per recipe

`Bill.GetWorkAmount(Thing)` is virtual — verified by compiling an override against the game assembly.
So recipes stay one per domain and the amount rides on the bill.

- **No `Trigger_Threshold` on the job** — a standing instruction like clear-area, nothing to
  calculate: flat **60 ticks**. (Not 10: with the walk and the job driver's ramp, 10 ticks would
  flicker the book in and out without ever being visible. 60 is ~1 second at 1x, ~1.4 minutes of
  game time, against 2500 ticks to the in-game hour.)
- **Threshold job** — scale by shortfall: `60 + (full - 60) × (1 - fill)`, full ≈ 1000–1200.
  Topping up from 95% is a scribbled note; rebuilding from empty is real planning.

Bill labels should come from `job.Tab.GetMainLabel(job)` and `GetSubLabel(job)` — the pair Colony
Manager Redux uses for its own job log — so a queue of plans is readable.

### The desk ladder

| Station | Work speed factor | Behaviour |
| --- | --- | --- |
| Managing spot | 0.5x | Specialist walks over, plans by hand |
| Basic manager desk | 1.0x | Same |
| Manager desk (computer) | 2.0x | Auto-fires; no bill, no book, no walk |
| AI manager | — | Free once trained, see below |

The work speed factors are already patched in (they restore Colony Manager Redux's 1000-vs-500 work
cost as a stat the bill system reads, and pick up Tool Cabinet links for free).

The computer desk skips the bill entirely rather than automating the paperwork: the planner holds
the plan for a processing delay equal to the work amount divided by the desk's speed factor, then
enacts it. Gated on `CompPowerTrader` being on and `CompBreakdownable` not broken — a blackout drops
the colony back to paperwork. Show progress in the desk's inspect string so designations don't
appear silently.

Consequence to accept deliberately: the whole expert-planning layer only applies while plans are
bills, so a computer desk switches it off on that map. That's the intended progression — early on
specialists spend their own time planning, later you buy that time back with power.

### Training the AI manager

A fresh AI manager starts untrained and does nothing by itself. While untrained it is the bill
giver: plans route to it rather than to a desk, and a colonist enacts them there while it watches.

**Three plans per domain**, not three overall. It handles hunting alone while still needing
supervision on mining, so the domain layer is what *builds* the AI instead of what the AI makes
obsolete. Roughly a week of supervised work for a typical five domains — proportionate to an AI
persona core.

**Routing rule, load-bearing:** while an untrained AI is powered, plans go to the AI, not to the
computer desk's auto-fire. Otherwise the desk does all the work and the AI never learns; the two
features deadlock on any colony with both.

An unpowered AI does **not** forget. Training is stored knowledge, persists through uninstall and
re-place, and losing it to a blackout would read as a bug.

Implementation notes, checked against the source:

- `Building_AIManager` is public and not sealed, and its `Tick` is `protected override` in 1.6, so a
  subclass swapped in by def patch can gate the autonomous loop behind `if (Trained) base.Tick();`
  with **no Harmony patch**.
- That loop starts `Manager.TryDoWork()` every 250 ticks while powered, runs the light green while
  working and red when idle. An untrained one sits on idle draw with a distinct amber light — the
  machine visibly not yet doing anything.
- It's a plain `Building`, not a `Building_WorkTable`, so `IBillGiver` must be implemented by hand:
  a `BillStack` field and four members.
- `CM_AIManager`'s def has no `hasInteractionCell`. Without one, pawns have nowhere to stand and
  simply never take the bill — it fails silently. The def patch must add the cell and offset
  alongside the thingClass swap.

## Unverified

Nothing in this mod has been exercised at runtime yet.

- Whether `JobDriver_DoBill` actually calls `Bill.GetWorkAmount` rather than reading
  `recipe.workAmount` directly. The signature is confirmed; the call site isn't. Fallback if it
  isn't used: a small set of tiered recipes per domain.
- Whether a recipe with no products completes cleanly — `Notify_IterationCompleted` fires and then
  there's nothing to store. This is the one path built from inference rather than read source.
