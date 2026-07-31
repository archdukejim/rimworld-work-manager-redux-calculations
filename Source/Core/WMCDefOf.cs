// WMCDefOf.cs
// Copyright (c) 2026 archdukejim

namespace ManagerBackgroundPlanning;

[DefOf]
public static class WMCDefOf
{
    /// <summary>The bill a manager works through to enact a plan.</summary>
    public static RecipeDef WMC_EnactManagementPlan = null!;

    /// <summary>The plan itself, sitting on the desk while it's being written.</summary>
    public static ThingDef WMC_UnfinishedManagementPlan = null!;

    static WMCDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(WMCDefOf));
}
