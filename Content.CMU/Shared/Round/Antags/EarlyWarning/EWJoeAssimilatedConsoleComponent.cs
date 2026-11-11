using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning;

/// <summary>
/// Marks a console the Early Warning Joe has assimilated. Phase E1 defined the marker so the
/// assimilate objective can read it; phase E2 drives it: the assimilation action adds it, the lock
/// system refuses the console's UI while it exists, and the repair chain removes it.
/// Networked (presence only, no component state) so the client can predict the UI refusal.
/// <see cref="AssimilatedBy"/> stays server-side because <see cref="EWJoeComponent"/> is
/// SendOnlyToOwner: the antag's identity must not leak through the consoles it touched.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class EWJoeAssimilatedConsoleComponent : Component
{
    /// <summary>The Joe that assimilated this console, if known. Server only (see the class note).</summary>
    [DataField]
    public EntityUid? AssimilatedBy;

    /// <summary>
    /// Repair progress: 0 = sealed (needs a screwdriver), 1 = panel open (needs wirecutters),
    /// 2 = wires cut (needs a core shard). The third step removes the component.
    /// </summary>
    [DataField]
    public int RepairStep;

    /// <summary>Do-after length of the two tool steps.</summary>
    [DataField]
    public TimeSpan ToolStepDelay = TimeSpan.FromSeconds(5);

    /// <summary>Do-after length of the shard step.</summary>
    [DataField]
    public TimeSpan ShardStepDelay = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Comms consoles only: when the next spoofed announcement goes out. Zero means never
    /// (every other console type).
    /// </summary>
    [DataField]
    public TimeSpan NextSpoof;

    /// <summary>Comms consoles only: time between spoofed announcements (D12: one per 5 min).</summary>
    [DataField]
    public TimeSpan SpoofInterval = TimeSpan.FromMinutes(5);

    /// <summary>Child effect entity drawn over the console while assimilated; deleted on repair.</summary>
    [DataField]
    public EntityUid? Effect;

    [DataField]
    public EntProtoId EffectPrototype = "CMUEWJoeConsoleCorruption";
}

/// <summary>Repair step carried through the do-after so completion re-validates the stage.</summary>
[Serializable, NetSerializable]
public sealed partial class EWJoeConsoleRepairDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public int Step;

    /// <summary>Required: DoAfterEvent is an implicit data definition and Robust skips types without one.</summary>
    public EWJoeConsoleRepairDoAfterEvent()
    {
    }

    public EWJoeConsoleRepairDoAfterEvent(int step)
    {
        Step = step;
    }
}
