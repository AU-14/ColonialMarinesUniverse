using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// FLOW-506: a marine can read a desecrated Clan Totem, with no living Bad Blood near it, for the grid square the
/// Lodge stands in and how close the clan is to calling its ship. Each desecration can be read once.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodTotemReadableComponent : Component
{
    /// <summary>
    /// How long the reading takes.
    /// </summary>
    [DataField]
    public TimeSpan ReadTime = TimeSpan.FromSeconds(25);

    /// <summary>
    /// The Lodge is given as a square this many tiles on a side.
    /// </summary>
    [DataField]
    public int GridSize = 10;

    /// <summary>
    /// This desecration has already been read. Reconsecrating the Totem clears it.
    /// </summary>
    [ViewVariables]
    public bool ReadThisDesecration;
}

[Serializable, NetSerializable]
public sealed partial class BadBloodTotemReadDoAfterEvent : SimpleDoAfterEvent;
