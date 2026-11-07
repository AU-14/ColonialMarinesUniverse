using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning;

/// <summary>The four Signal wheels the Early Warning Joe's abilities are sorted into (action-bar report §4).</summary>
[Serializable, NetSerializable]
public enum EWJoeAbilityGroup : byte
{
    /// <summary>Test of Faith / Judgement, Optical Zoom, Sense Penitent.</summary>
    Gaze,

    /// <summary>Drown, Leg Sweep, Slam, Choke, Throw.</summary>
    Hands,

    /// <summary>Disrupt, Assimilate, Scour, Unwind.</summary>
    Machine,

    /// <summary>Overclock, Emergency Coolant, Consecrate, Z-Level Leap, Saturate.</summary>
    Engine,
}

/// <summary>
/// On an Early Warning Joe ability's action prototype: the ability lives in this Signal wheel instead of on
/// the hotbar. The action itself is unchanged (cooldown, targeting, server checks); the client keeps it off
/// the bar and lists it in the wheel. A player can still drag it onto the bar from the abilities window.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeAbilityGroupComponent : Component
{
    [DataField(required: true)]
    public EWJoeAbilityGroup Group;
}

/// <summary>Opens one Signal wheel. Raised only on the Joe's own client (client-exclusive action).</summary>
public sealed partial class EWJoeOpenWheelActionEvent : InstantActionEvent
{
    [DataField(required: true)]
    public EWJoeAbilityGroup Group;
}
