using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared._RMC14.Marines.Roles.Ranks;

namespace Content.Shared._AU14.Marines.Roles.Ranks;

/// <summary>
/// When equipped or inserted into a uniform accessory slot, overrides the entity's rank.
/// Reverts to the job-assigned rank when removed.
/// Mirrors JobTitleChangerComponent but for the rank system.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class RankChangerComponent : Component
{
    [DataField(required: true)]
    public ProtoId<RankPrototype> Rank;

    [DataField]
    public bool Override = true;

    [DataField]
    public bool Applied = false;
}