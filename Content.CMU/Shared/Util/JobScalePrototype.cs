using System;
using Robust.Shared.Prototypes;

namespace Content.Shared.AU14.util;

/// <summary>
/// Per-job scaling info.
/// <para>Scale: extra slots per player (e.g. 0.5 = 1 extra slot per 2 players).</para>
/// <para>Benchmark: if set, this is the base slot count; if null, scaling is applied on top of existing slots.</para>
/// <para>Maximum: if set, final slots are capped at this value.</para>
/// <para>WhenToBeginScaling: the player-count threshold at which scaling begins. Scaling only
/// considers players ABOVE this threshold (inclusive check). For example, if
/// WhenToBeginScaling = 30 and Scale = 0.16, then at 50 players extra = floor((50 - 30) * 0.16).</para>
/// <para>When Benchmark is set:  finalSlots = Benchmark + floor(max(0, playerCount - WhenToBeginScaling) * Scale)</para>
/// <para>When Benchmark is null: finalSlots = existingSlots + floor(max(0, playerCount - WhenToBeginScaling) * Scale)</para>
/// </summary>
[DataRecord]
public readonly record struct JobScaleEntry(float Scale, int WhenToBeginScaling, int? Benchmark = null, int? Maximum = null);

public static class JobScaling
{
    public static int CalculateExtraSlots(int playerCount, JobScaleEntry entry)
    {
        if (playerCount <= 0)
            return 0;

        var effectivePlayers = Math.Max(0, playerCount - entry.WhenToBeginScaling);
        return (int) Math.Floor(effectivePlayers * entry.Scale);
    }

    public static int CalculateScaledSlots(int playerCount, int baseSlots, JobScaleEntry entry)
    {
        var baseline = entry.Benchmark ?? baseSlots;
        var scaled = baseline + CalculateExtraSlots(playerCount, entry);
        if (entry.Maximum != null)
            scaled = Math.Min(scaled, entry.Maximum.Value);

        return scaled;
    }
}

/// <summary>
/// A single prototype that declares scaling rules for multiple jobs.
/// The dictionary key is the job prototype ID (e.g. "AU14JobGOVFORSquadRifleman").
/// </summary>
[Prototype]
public sealed partial class JobScalePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Dictionary of job prototype ID → scaling info.
    /// </summary>
    [DataField("jobs", required: true)]
    public Dictionary<string, JobScaleEntry> Jobs { get; private set; } = new();
}

