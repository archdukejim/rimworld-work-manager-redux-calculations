// BackgroundPlanner.cs
// Copyright (c) 2026 archdukejim

using ilyvion.Laboratory;
using ilyvion.Laboratory.Coroutines;

namespace ManagerBackgroundPlanning;

/// <summary>
/// Does the colony's thinking on a clock instead of waiting for a colonist to sit down, and posts
/// the result to the manager desk as a bill.
/// </summary>
/// <remarks>
/// At each anchored planning slot the due manager jobs are marked outstanding and Colony Manager
/// Redux's gather phase — which reads the map but changes nothing — runs in the background. What
/// it works out is parked on a <see cref="Bill_ManagementPlan"/> until a manager works through it.
/// </remarks>
public sealed class BackgroundPlanner : MapComponent
{
    /// <summary>Hours are 2500 ticks; checking once a game-minute is far more than enough.</summary>
    private const int CheckInterval = 60;

    private int _lastHour = -1;
    private CoroutineHandle? _gatherHandle;
    private AnyBoxed<JobTracker.PendingJobWork?>? _gathered;
    private string _gatheringFor = string.Empty;

    public BackgroundPlanner(Map map)
        : base(map) { }

    private static BackgroundPlanningSettings Settings => BackgroundPlanningMod.Settings;

    public override void MapComponentTick()
    {
        if (Find.TickManager.TicksGame % CheckInterval != 0)
        {
            return;
        }

        CoroutineBoost.Poll();
        CollectFinishedGather();

        var hour = GenLocalDate.HourOfDay(map);
        if (hour == _lastHour)
        {
            return;
        }

        _lastHour = hour;
        OnHourElapsed(hour);
    }

    private void OnHourElapsed(int hour)
    {
        ExpireOverduePlans();
        DestroyOrphanedPlanBooks();

        var dueJobs = MarkAnchoredJobsDue(hour);
        var billNeedingPlan = FindOurBills().FirstOrDefault(b => b.NeedsPayload);

        if (dueJobs == 0 && billNeedingPlan == null)
        {
            return;
        }

        StartGather();
    }

    /// <summary>
    /// Marks every managed job whose anchored slot is this hour as outstanding, which is what makes
    /// Colony Manager Redux's gather phase pick it up. Returns how many were marked.
    /// </summary>
    private int MarkAnchoredJobsDue(int hour)
    {
        var day = GenLocalDate.DayOfYear(map);
        var anchor = Settings.AnchorHour;
        var marked = 0;

        foreach (var job in Manager.For(map).JobTracker.Jobs)
        {
            if (!job.IsManaged || job.IsSuspended)
            {
                continue;
            }

            if (!PlanSchedule.IsAnchorSlot(hour, day, anchor, job.UpdateInterval.Ticks))
            {
                continue;
            }

            job.Untouch();
            marked++;
        }

        return marked;
    }

    private void StartGather()
    {
        if (_gatherHandle is { IsCompleted: false })
        {
            return;
        }

        var station = BestStation();
        if (station == null)
        {
            PlanningLog.MessageOnce(
                "no-station-" + map.uniqueID,
                $"No usable manager station on {map}; nothing will be planned there until one is "
                    + "built and powered."
            );
            return;
        }

        var gathered = new AnyBoxed<JobTracker.PendingJobWork?>(null);
        Coroutine? coroutine;
        try
        {
            coroutine = Manager.For(map).TryGatherWork(gathered);
        }
        catch (Exception exception)
        {
            PlanningLog.Error("Background planning failed to start: " + exception);
            return;
        }

        if (coroutine == null)
        {
            // Nothing was actually due; the anchor came round on a quiet day.
            return;
        }

        _gathered = gathered;
        _gatheringFor = station.Label;
        CoroutineBoost.Begin();
        _gatherHandle = MultiTickCoroutineManager.StartCoroutine(
            coroutine,
            debugHandle: "WMC.BackgroundGather"
        );
    }

    private void CollectFinishedGather()
    {
        if (_gatherHandle == null || !_gatherHandle.IsCompleted)
        {
            return;
        }

        _gatherHandle = null;
        CoroutineBoost.End();

        var payload = _gathered?.Value;
        _gathered = null;

        if (payload == null)
        {
            return;
        }

        var existing = FindOurBills().FirstOrDefault(b => b.NeedsPayload);
        if (existing != null)
        {
            // A bill that came back from a save without its plan; give it this one rather than
            // stacking a second bill on the desk.
            existing.Attach(payload, PlanLabel(), NextSlotTick());
            return;
        }

        PostBill(payload);
    }

    private void PostBill(JobTracker.PendingJobWork payload)
    {
        var station = BestStation();
        if (station == null)
        {
            PlanningLog.Warning(
                "Finished planning but there's no longer a usable manager station to post it to."
            );
            return;
        }

        // Only one plan waits at a time: a new one supersedes whatever nobody got round to.
        foreach (var stale in FindOurBills().ToList())
        {
            RemoveBill(stale);
        }

        var bill = new Bill_ManagementPlan(WMCDefOf.WMC_EnactManagementPlan);
        station.billStack.AddBill(bill);
        bill.Attach(payload, PlanLabel(), NextSlotTick());
    }

    private string PlanLabel() =>
        _gatheringFor.NullOrEmpty() ? map.Parent?.Label ?? "colony" : _gatheringFor;

    /// <summary>
    /// When the next plan is due, which is when this one stops being worth enacting. Uses the
    /// shortest managed interval, since that's what will produce the next plan.
    /// </summary>
    private int NextSlotTick()
    {
        var shortest = int.MaxValue;
        foreach (var job in Manager.For(map).JobTracker.Jobs)
        {
            if (job.IsManaged && !job.IsSuspended)
            {
                shortest = Math.Min(shortest, job.UpdateInterval.Ticks);
            }
        }

        if (shortest == int.MaxValue)
        {
            shortest = GenDate.TicksPerDay;
        }

        return Find.TickManager.TicksGame
            + PlanSchedule.TicksToNextSlot(map, Settings.AnchorHour, shortest);
    }

    private void ExpireOverduePlans()
    {
        foreach (var bill in FindOurBills().ToList())
        {
            if (bill.Expired)
            {
                RemoveBill(bill);
            }
        }
    }

    /// <summary>Deletes a bill and the half-written plan on the desk that belongs to it.</summary>
    private void RemoveBill(Bill_ManagementPlan bill)
    {
        DestroyBooksFor(bill);
        bill.billStack?.Delete(bill);
    }

    private void DestroyBooksFor(Bill bill)
    {
        foreach (var book in PlanBooks().ToList())
        {
            if (book.BoundBill == bill)
            {
                book.Destroy(DestroyMode.Vanish);
            }
        }
    }

    /// <summary>
    /// Cleans up plans left on the desk with no bill behind them — a cancelled bill, or one that
    /// expired while a colonist was halfway through it.
    /// </summary>
    private void DestroyOrphanedPlanBooks()
    {
        foreach (var book in PlanBooks().ToList())
        {
            var bill = book.BoundBill;
            if (bill == null || bill.DeletedOrDereferenced)
            {
                book.Destroy(DestroyMode.Vanish);
            }
        }
    }

    private IEnumerable<UnfinishedThing> PlanBooks() =>
        map.listerThings.ThingsOfDef(WMCDefOf.WMC_UnfinishedManagementPlan).OfType<UnfinishedThing>();

    private IEnumerable<Bill_ManagementPlan> FindOurBills()
    {
        foreach (var station in Stations())
        {
            foreach (var bill in station.billStack.Bills.OfType<Bill_ManagementPlan>())
            {
                yield return bill;
            }
        }
    }

    private IEnumerable<Building_ManagerStation> Stations() =>
        map.listerBuildings.AllBuildingsColonistOfClass<Building_ManagerStation>();

    /// <summary>
    /// The station plans get posted to: the fastest usable one, since that's where a manager will
    /// get through the paperwork quickest.
    /// </summary>
    private Building_ManagerStation? BestStation() =>
        Stations()
            .Where(Usable)
            .OrderBy(s => s.GetComp<CompManagerStation>()?.Props.speed ?? int.MaxValue)
            .FirstOrDefault();

    private bool Usable(Building_ManagerStation station)
    {
        if (station.GetComp<CompManagerStation>() == null || station.IsBurning())
        {
            return false;
        }

        if (!Settings.RequirePower)
        {
            return true;
        }

        var power = station.TryGetComp<CompPowerTrader>();
        return power == null || power.PowerOn;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref _lastHour, "lastHour", -1);
    }
}
