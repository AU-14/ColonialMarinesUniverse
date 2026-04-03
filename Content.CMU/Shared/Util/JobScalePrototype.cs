using Robust.Shared.Prototypes;

namespace Content.Shared.AU14.util;

/// <summary>
/// Per-job scaling info.
/// <para>Scale: extra slots per player beyond WhenToBeginScaling (e.g. 0.5 = 1 extra slot per 2 players).</para>
/// <para>Benchmark: if set, overrides the base slot count; if null, scaling is applied on top of existing slots.</para>
/// <para>WhenToBeginScaling: player count threshold before scaling kicks in.</para>
/// <para>When Benchmark is set:  finalSlots = Benchmark + floor((playerCount - WhenToBeginScaling) * Scale)</para>
/// <para>When Benchmark is null: finalSlots = existingSlots + floor((playerCount - WhenToBeginScaling) * Scale)</para>
/// </summary>
[DataRecord]
public readonly record struct JobScaleEntry(float Scale, int WhenToBeginScaling, int? Benchmark = null);

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

