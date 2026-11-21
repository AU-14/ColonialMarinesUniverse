using Content.Shared._RMC14.Marines.Roles.Ranks;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Roles.Ranks;

/// <summary>
///     Restricts this entity's user interfaces to users holding one of <see cref="AllowedRanks"/>.
///     Checks the user's own rank, not their ID card, so a stolen ID does not grant access.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMURankLockSystem))]
public sealed partial class CMURankLockComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public HashSet<ProtoId<RankPrototype>> AllowedRanks = new();

    [DataField]
    public LocId DeniedPopup = "cmu-rank-lock-denied";
}
