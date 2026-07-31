// Bill_ManagementPlan.cs
// Copyright (c) 2026 archdukejim

using ilyvion.Laboratory.Coroutines;

namespace ManagerBackgroundPlanning;

/// <summary>
/// A management plan waiting to be signed off, posted to the manager desk as an ordinary bill.
/// </summary>
/// <remarks>
/// The plan's decisions were already worked out in the background; this bill carries them and
/// exists so a colonist has to spend real time at the desk before they take effect. Because the
/// gathered work is live coroutine state, it can't be written to a save — a bill that survives a
/// reload comes back without its plan and asks the background planner for a fresh one before it
/// can be worked.
/// </remarks>
/// <remarks>
/// Derives from <see cref="Bill_ProductionWithUft"/> rather than <see cref="Bill_Production"/>
/// because vanilla casts the bill to that type when it creates the unfinished thing; a plain
/// production bill with an unfinishedThingDef throws the moment a pawn starts work.
/// </remarks>
public class Bill_ManagementPlan : Bill_ProductionWithUft
{
    private JobTracker.PendingJobWork? _payload;
    private int _createdTick = -1;
    private int _expiresTick = -1;
    private string _planLabel = string.Empty;

    public Bill_ManagementPlan() { }

    public Bill_ManagementPlan(RecipeDef recipe)
        : base(recipe, null) { }

    /// <summary>True when this bill has no plan to enact and needs one gathered for it.</summary>
    public bool NeedsPayload => _payload == null;

    public bool Expired => _expiresTick > 0 && Find.TickManager.TicksGame >= _expiresTick;

    public int ExpiresTick => _expiresTick;

    public override string Label =>
        _planLabel.NullOrEmpty()
            ? base.Label
            : "WMC.Bill.Label".Translate(_planLabel);

    /// <summary>Hands this bill the plan it should enact.</summary>
    public void Attach(JobTracker.PendingJobWork payload, string planLabel, int expiresTick)
    {
        _payload = payload;
        _planLabel = planLabel;
        _createdTick = Find.TickManager.TicksGame;
        _expiresTick = expiresTick;
        repeatMode = BillRepeatModeDefOf.RepeatCount;
        repeatCount = 1;
    }

    /// <summary>
    /// A pawn may only work on a plan that actually has decisions in it and hasn't been overtaken
    /// by the next scheduled one.
    /// </summary>
    public override bool ShouldDoNow() => !NeedsPayload && !Expired && base.ShouldDoNow();

    public override void Notify_IterationCompleted(Pawn billDoer, List<Thing> ingredients)
    {
        var payload = _payload;
        _payload = null;

        base.Notify_IterationCompleted(billDoer, ingredients);

        if (payload == null)
        {
            return;
        }

        var map = billDoer?.Map ?? (billStack?.billGiver as Thing)?.Map;
        if (map == null)
        {
            PlanningLog.Warning("A management plan finished with no map to apply it to.");
            return;
        }

        PlanApplication.Apply(map, payload);
        GrantSocialExperience(billDoer);
    }

    /// <summary>
    /// The recipe's work skill grants Intellectual experience while the pawn writes; managing is
    /// as much a social job as an intellectual one, so Social is paid out on completion.
    /// </summary>
    private void GrantSocialExperience(Pawn? billDoer)
    {
        if (billDoer?.skills == null)
        {
            return;
        }

        var share = BackgroundPlanningMod.Settings.SocialXpShare;
        if (share <= 0f)
        {
            return;
        }

        // 0.11 experience per tick of work is the rate vanilla pays the recipe's own work skill.
        var experience = recipe.workAmount * 0.11f * share;
        billDoer.skills.Learn(SkillDefOf.Social, experience);
    }

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look(ref _createdTick, "wmcCreatedTick", -1);
        Scribe_Values.Look(ref _expiresTick, "wmcExpiresTick", -1);
        Scribe_Values.Look(ref _planLabel, "wmcPlanLabel", string.Empty);

        // _payload deliberately isn't saved: it holds a live coroutine's gathered state. The
        // planner notices the empty bill after loading and gathers a fresh plan for it.
    }
}

/// <summary>Runs a gathered plan's decisions against the map.</summary>
public static class PlanApplication
{
    /// <summary>
    /// Applies the plan. Colony Manager Redux's execute phase is a coroutine, so this runs it at
    /// the boosted background pace, which finishes it within a tick or two rather than spreading
    /// designations out over a minute of game time.
    /// </summary>
    public static void Apply(Map map, JobTracker.PendingJobWork payload)
    {
        try
        {
            var coroutine = Manager.For(map).TryExecuteWork(payload);
            if (coroutine == null)
            {
                return;
            }

            CoroutineBoost.Begin();
            var handle = MultiTickCoroutineManager.StartCoroutine(
                coroutine,
                debugHandle: "WMC.EnactPlan"
            );
            CoroutineBoost.EndWhenComplete(handle);
        }
        catch (Exception exception)
        {
            CoroutineBoost.End();
            PlanningLog.Error("Failed to enact a management plan: " + exception);
        }
    }
}
