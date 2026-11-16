using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// FLOW-507 (I3): the clan star chart that turns up at the Lodge armory when the ship is called.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodStarChartComponent : Component
{
    /// <summary>
    /// A non-Bad Blood has picked it up. The recovery is only counted once.
    /// </summary>
    [DataField]
    public bool Recovered;
}

/// <summary>
/// FLOW-507 (I3): the Lodge's trophy rack can be raided. Put on the Lodge the first time someone tries, as lodge.yml
/// belongs to the Lodge core; a value set there in YAML is kept.
/// </summary>
[RegisterComponent]
public sealed partial class BadBloodLodgeRaidComponent : Component
{
    [DataField]
    public TimeSpan StripTime = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Intel points a stripped trophy is worth once carried into a retrieve-item area.
    /// </summary>
    [DataField]
    public FixedPoint2 TrophyRetrieveValue = FixedPoint2.New(0.5);
}

[Serializable, NetSerializable]
public sealed partial class BadBloodStripTrophyDoAfterEvent : SimpleDoAfterEvent;
