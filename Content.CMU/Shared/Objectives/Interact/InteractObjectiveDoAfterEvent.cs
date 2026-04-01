using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.AU14.Objectives.Interact;

[Serializable, NetSerializable]
public sealed partial class InteractObjectiveDoAfterEvent : SimpleDoAfterEvent
{
    /// <summary>
    /// The faction of the user performing the interaction.
    /// </summary>
    public string Faction = string.Empty;

    /// <summary>
    /// The entity being interacted with (the target).
    /// </summary>
    public NetEntity InteractTarget;
}


