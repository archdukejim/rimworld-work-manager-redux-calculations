// BackgroundPlanningMod.cs
// Copyright (c) 2026 archdukejim

namespace ManagerBackgroundPlanning;

/// <summary>Mod entry point: applies the Harmony patches and hosts the settings page.</summary>
public sealed class BackgroundPlanningMod : Mod
{
    public static BackgroundPlanningSettings Settings { get; private set; } = new();

    public BackgroundPlanningMod(ModContentPack content)
        : base(content)
    {
        Settings = GetSettings<BackgroundPlanningSettings>();

        HarmonyPatches.Apply(new Harmony("archdukejim.managerbackgroundplanning"));
    }

    public override string SettingsCategory() => "Background Planning";

    public override void DoSettingsWindowContents(Rect inRect)
    {
        var listing = new Listing_Standard();
        listing.Begin(inRect);

        listing.Label(
            "WMC.Settings.AnchorHour".Translate(PlanSchedule.HourLabel(Settings.AnchorHour))
        );
        listing.Label("WMC.Settings.AnchorHour.Tip".Translate(), -1f, null);
        Settings.AnchorHour = Mathf.RoundToInt(
            listing.Slider(Settings.AnchorHour, 0f, 23f)
        );
        listing.Label("WMC.Settings.AnchorExample".Translate(PlanSchedule.ExampleTimes(Settings.AnchorHour, 4)));

        listing.Gap();

        listing.CheckboxLabeled(
            "WMC.Settings.ReplaceDeskPlanning".Translate(),
            ref Settings.ReplaceDeskPlanning,
            "WMC.Settings.ReplaceDeskPlanning.Tip".Translate()
        );
        listing.CheckboxLabeled(
            "WMC.Settings.RequirePower".Translate(),
            ref Settings.RequirePower,
            "WMC.Settings.RequirePower.Tip".Translate()
        );

        listing.Gap();

        listing.Label("WMC.Settings.Speed".Translate(Settings.BackgroundSpeed.ToString("0")));
        listing.Label("WMC.Settings.Speed.Tip".Translate(), -1f, null);
        Settings.BackgroundSpeed = Mathf.Round(listing.Slider(Settings.BackgroundSpeed, 1f, 50f));

        listing.Gap();

        listing.Label("WMC.Settings.SocialShare".Translate(Settings.SocialXpShare.ToStringPercent()));
        Settings.SocialXpShare = listing.Slider(Settings.SocialXpShare, 0f, 1f);

        listing.End();
    }
}

/// <summary>Everything the player can tune about when and how fast planning happens.</summary>
public sealed class BackgroundPlanningSettings : ModSettings
{
    /// <summary>Hour of day every planning schedule is anchored to.</summary>
    public int AnchorHour = 4;

    /// <summary>Take over Colony Manager Redux's at-the-desk planning entirely.</summary>
    public bool ReplaceDeskPlanning = true;

    /// <summary>Only plan in the background when a usable, powered station exists.</summary>
    public bool RequirePower = true;

    /// <summary>
    /// How much faster background planning runs than Colony Manager Redux's normal pacing. The
    /// point is to get it over with in a burst while nothing else is happening, rather than
    /// trickling work across thousands of ticks.
    /// </summary>
    public float BackgroundSpeed = 20f;

    /// <summary>Social experience granted on finishing a plan, as a share of the Intellectual
    /// experience the work itself grants.</summary>
    public float SocialXpShare = 0.5f;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref AnchorHour, "anchorHour", 4);
        Scribe_Values.Look(ref ReplaceDeskPlanning, "replaceDeskPlanning", true);
        Scribe_Values.Look(ref RequirePower, "requirePower", true);
        Scribe_Values.Look(ref BackgroundSpeed, "backgroundSpeed", 20f);
        Scribe_Values.Look(ref SocialXpShare, "socialXpShare", 0.5f);
    }
}

/// <summary>Prefixed logging so this mod's output is identifiable in a modded log.</summary>
public static class PlanningLog
{
    private const string Prefix = "[Background Planning] ";

    private static readonly HashSet<string> OnceKeys = [];

    public static void Message(string message) => Log.Message(Prefix + message);

    public static void Warning(string message) => Log.Warning(Prefix + message);

    public static void Error(string message) => Log.Error(Prefix + message);

    public static void MessageOnce(string key, string message)
    {
        if (OnceKeys.Add(key))
        {
            Message(message);
        }
    }
}
