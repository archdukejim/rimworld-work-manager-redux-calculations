// PlanSchedule.cs
// Copyright (c) 2026 archdukejim

namespace ManagerBackgroundPlanning;

/// <summary>
/// Turns a manager job's update interval into fixed times of day.
/// </summary>
/// <remarks>
/// Colony Manager Redux runs a job when its interval has elapsed since it last ran, so a "every
/// four hours" job drifts to whenever the last run happened to finish. Here the schedule is
/// anchored: with a 4am anchor, a four-hour job is planned at 4am, 8am, noon, 4pm, 8pm and
/// midnight, every day, no matter when it last ran. Anything longer than a day fires at the anchor
/// hour every N days.
/// </remarks>
public static class PlanSchedule
{
    public const int HoursPerDay = 24;

    private static int IntervalHours(int intervalTicks) =>
        Mathf.Max(1, Mathf.RoundToInt(intervalTicks / (float)GenDate.TicksPerHour));

    /// <summary>Whether <paramref name="hour"/> on <paramref name="day"/> is a planning slot.</summary>
    public static bool IsAnchorSlot(int hour, int day, int anchorHour, int intervalTicks)
    {
        var intervalHours = IntervalHours(intervalTicks);

        if (intervalHours >= HoursPerDay)
        {
            // Daily or longer: only the anchor hour counts, every Nth day.
            if (hour != anchorHour)
            {
                return false;
            }
            var intervalDays = Mathf.Max(1, Mathf.RoundToInt(intervalHours / (float)HoursPerDay));
            return Mod(day, intervalDays) == 0;
        }

        return Mod(hour - anchorHour, HoursPerDay) % intervalHours == 0;
    }

    /// <summary>
    /// Ticks from now until the next planning slot for this interval, used to expire a plan
    /// exactly when its replacement is due.
    /// </summary>
    public static int TicksToNextSlot(Map map, int anchorHour, int intervalTicks)
    {
        var hour = GenLocalDate.HourOfDay(map);
        var day = GenLocalDate.DayOfYear(map);

        for (var ahead = 1; ahead <= HoursPerDay * 15; ahead++)
        {
            var futureHour = Mod(hour + ahead, HoursPerDay);
            var futureDay = day + ((hour + ahead) / HoursPerDay);
            if (IsAnchorSlot(futureHour, futureDay, anchorHour, intervalTicks))
            {
                return ahead * GenDate.TicksPerHour;
            }
        }

        // Nothing found within a fortnight; fall back to the raw interval.
        return Mathf.Max(GenDate.TicksPerHour, intervalTicks);
    }

    /// <summary>"4am", "1pm" — for the settings page.</summary>
    public static string HourLabel(int hour)
    {
        var suffix = hour < 12 ? "am" : "pm";
        var display = hour % 12;
        if (display == 0)
        {
            display = 12;
        }
        return display + suffix;
    }

    /// <summary>A readable list of the slots an interval would produce, for the settings page.</summary>
    public static string ExampleTimes(int anchorHour, int intervalHours)
    {
        var slots = new List<string>();
        for (var hour = 0; hour < HoursPerDay; hour++)
        {
            var slot = Mod(anchorHour + (hour * intervalHours), HoursPerDay);
            if (slots.Count >= HoursPerDay / Mathf.Max(1, intervalHours))
            {
                break;
            }
            slots.Add(HourLabel(slot));
        }
        return string.Join(", ", slots);
    }

    /// <summary>Modulo that stays positive for negative inputs, unlike C#'s remainder.</summary>
    private static int Mod(int value, int modulus) => ((value % modulus) + modulus) % modulus;
}
