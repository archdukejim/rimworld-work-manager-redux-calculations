// HarmonyPatches.cs
// Copyright (c) 2026 archdukejim

using System.Reflection;
using ilyvion.Laboratory.Coroutines;

namespace ManagerBackgroundPlanning;

/// <summary>
/// The two things this mod changes about Colony Manager Redux: colonists no longer do the thinking
/// at the desk, and background thinking is allowed to run flat out.
/// </summary>
/// <remarks>
/// Patched by hand rather than by attribute so a signature that has moved in a future version of
/// Colony Manager Redux logs a clear warning and leaves the rest of the mod working, instead of
/// throwing during startup.
/// </remarks>
public static class HarmonyPatches
{
    public static void Apply(Harmony harmony)
    {
        PatchDeskManaging(harmony);
        PatchCoroutinePacing(harmony);
    }

    private static void PatchDeskManaging(Harmony harmony)
    {
        var target = AccessTools.Method("ColonyManagerRedux.WorkGiver_Manage:HasJobOnThing");
        if (target == null)
        {
            PlanningLog.Warning(
                "Couldn't find WorkGiver_Manage.HasJobOnThing; colonists will keep doing Colony "
                    + "Manager Redux's own at-the-desk planning alongside this mod's plans."
            );
            return;
        }

        harmony.Patch(
            target,
            prefix: new HarmonyMethod(typeof(HarmonyPatches), nameof(SuppressDeskManaging))
        );
    }

    private static void PatchCoroutinePacing(Harmony harmony)
    {
        var operations = AccessTools.Method(
            typeof(ColonyManagerRedux.Settings),
            "GetOperationsPerTickForCoroutine"
        );
        var ticksBetween = AccessTools.Method(
            typeof(ColonyManagerRedux.Settings),
            "GetTicksBetweenOperationsForCoroutine"
        );

        if (operations == null || ticksBetween == null)
        {
            PlanningLog.Warning(
                "Couldn't find Colony Manager Redux's coroutine pacing settings; background "
                    + "planning will run at the mod's normal pace instead of in a burst."
            );
            return;
        }

        harmony.Patch(
            operations,
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(BoostOperationsPerTick))
        );
        harmony.Patch(
            ticksBetween,
            postfix: new HarmonyMethod(typeof(HarmonyPatches), nameof(BoostTicksBetween))
        );
    }

    /// <summary>
    /// Stops Colony Manager Redux from sending colonists to the desk to work jobs out from
    /// scratch. Under this mod that work has already happened; what's left is enacting it, which
    /// is a bill.
    /// </summary>
    private static bool SuppressDeskManaging(ref bool __result)
    {
        if (!BackgroundPlanningMod.Settings.ReplaceDeskPlanning)
        {
            return true;
        }

        __result = false;
        return false;
    }

    private static void BoostOperationsPerTick(ref int __result)
    {
        if (!CoroutineBoost.Active)
        {
            return;
        }

        var boosted = __result * BackgroundPlanningMod.Settings.BackgroundSpeed;
        __result = Mathf.Clamp(Mathf.RoundToInt(boosted), 1, 100000);
    }

    private static void BoostTicksBetween(ref int __result)
    {
        if (!CoroutineBoost.Active)
        {
            return;
        }

        // One tick is the floor: the coroutine machinery still needs to yield, it just shouldn't
        // sit idle for ten ticks between batches when nothing else is competing for the time.
        __result = 1;
    }
}

/// <summary>
/// Marks the windows during which planning coroutines should run flat out. Global rather than
/// per-coroutine because Colony Manager Redux reads its pacing from mod settings deep inside the
/// coroutine bodies, with nothing to hang a per-run value off.
/// </summary>
public static class CoroutineBoost
{
    private static int _depth;
    private static readonly List<CoroutineHandle> Watched = [];

    public static bool Active => _depth > 0;

    public static void Begin() => _depth++;

    public static void End() => _depth = Math.Max(0, _depth - 1);

    /// <summary>Keeps the boost on until <paramref name="handle"/> reports completion.</summary>
    public static void EndWhenComplete(CoroutineHandle handle) => Watched.Add(handle);

    /// <summary>Releases the boost for any watched coroutine that has finished.</summary>
    public static void Poll()
    {
        for (var i = Watched.Count - 1; i >= 0; i--)
        {
            if (Watched[i].IsCompleted)
            {
                Watched.RemoveAt(i);
                End();
            }
        }
    }

    /// <summary>Drops all boost state; used when a game is unloaded.</summary>
    public static void Reset()
    {
        Watched.Clear();
        _depth = 0;
    }
}
