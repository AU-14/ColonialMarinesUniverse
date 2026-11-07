using Content.Shared._RMC14.Stun;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Fireman;

/// <summary>
/// On a grabber: lets it aggressively grab xenomorphs whose FiremanCarriable carrier whitelist (Yautja only) would
/// refuse it: castes up to tier 3 and size Big (praetorian, boiler, despoiler...), never the heavies (FLOW-02). Checked
/// in FiremanCarrySystem.ValidateAggressiveGrab next to that whitelist. A xeno target must pass every rule: the
/// whitelist, not the blacklist, tier and mob size.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUXenoGrabberComponent : Component
{
    /// <summary>
    /// Optional extra whitelist a xeno must pass. None by default: tier and size decide, and the pull itself is let
    /// through for exactly the same castes (<see cref="CMUPullParalyzeImmuneEvent"/>), so the grab and the pull agree.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityWhitelist? Whitelist;

    /// <summary>
    /// Castes refused whatever their tier and size. The Yautja abomination is a boss-class heavy that is tier 1 and
    /// size Big, so tier and size alone would let it through.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityWhitelist? Blacklist = new() { Components = ["YautjaAbomination"] };

    /// <summary>Highest xeno tier that can be grabbed. The queen and king are tier 0, so size gates them.</summary>
    [DataField, AutoNetworkedField]
    public int MaxTier = 3;

    /// <summary>
    /// Largest RMC mob size that can be grabbed. Big (praetorian, boiler, despoiler, ravager, carrier...) can be;
    /// Immobile, the heavies (crusher, queen, king), never.
    /// </summary>
    [DataField, AutoNetworkedField]
    public RMCSizes MaxSize = RMCSizes.Big;

    /// <summary>The xeno currently held under the hold cap, if any. Set by the owner's hold-cap system.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? HoldTarget;

    /// <summary>When the current xeno hold is forced to end.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan HoldUntil;

    /// <summary>No xeno can be aggressively grabbed through this component before this time.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextGrab;
}
