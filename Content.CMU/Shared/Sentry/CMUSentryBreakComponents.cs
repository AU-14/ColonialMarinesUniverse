using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Sentry;

/// <summary>
///     A sentry that breaks down instead of being destroyed. It can still be packed up and deployed while broken,
///     and has to be repaired step by step once deployed.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUSentryBreakableComponent : Component
{
    /// <summary>Share of the ammo left in the magazine that's lost when it breaks.</summary>
    [DataField]
    public float AmmoLoss = 0.25f;
}

/// <summary>
///     A broken sentry. <see cref="Step"/> counts how many repair steps are done.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUSentryBrokenComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Step;
}

[Serializable, NetSerializable]
public sealed partial class CMUSentryRepairDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public int Step;

    public CMUSentryRepairDoAfterEvent(int step)
    {
        Step = step;
    }

    public CMUSentryRepairDoAfterEvent()
    {
    }
}
