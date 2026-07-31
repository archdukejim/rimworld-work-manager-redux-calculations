// ManagementPlanBook.cs
// Copyright (c) 2026 archdukejim

using System.Text;

namespace ManagerBackgroundPlanning;

/// <summary>
/// The half-written plan on the desk.
/// </summary>
/// <remarks>
/// Everything that makes it behave the way an unfinished item should — holding work across
/// interruptions, refusing to be hauled, costing and returning nothing — comes from
/// <see cref="UnfinishedThing"/> and the def it's built from. What's added here is a way to throw
/// away the draft without throwing away the decisions: discarding the book leaves the bill on the
/// desk, so a manager starts the write-up again rather than the colony having to think it all
/// through from scratch.
/// </remarks>
public class ManagementPlanBook : UnfinishedThing
{
    public override IEnumerable<Gizmo> GetGizmos()
    {
        foreach (var gizmo in base.GetGizmos())
        {
            yield return gizmo;
        }

        yield return new Command_Action
        {
            defaultLabel = "WMC.Book.Discard".Translate(),
            defaultDesc = "WMC.Book.Discard.Tip".Translate(),
            icon = TexCommand.ClearPrioritizedWork,
            action = () =>
            {
                if (!Destroyed)
                {
                    Destroy(DestroyMode.Vanish);
                }
            },
        };
    }

    public override string GetInspectString()
    {
        var builder = new StringBuilder();
        var baseString = base.GetInspectString();
        if (!baseString.NullOrEmpty())
        {
            builder.AppendLine(baseString);
        }

        if (BoundBill is Bill_ManagementPlan plan)
        {
            if (plan.NeedsPayload)
            {
                builder.AppendLine("WMC.Book.AwaitingPlan".Translate());
            }
            else if (plan.ExpiresTick > 0)
            {
                var remaining = plan.ExpiresTick - Find.TickManager.TicksGame;
                builder.AppendLine(
                    remaining > 0
                        ? "WMC.Book.Expires".Translate(remaining.ToStringTicksToPeriod())
                        : "WMC.Book.Expired".Translate()
                );
            }
        }

        return builder.ToString().TrimEndNewlines();
    }
}
