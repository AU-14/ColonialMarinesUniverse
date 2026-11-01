using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Marines.LastStand;

/// <summary>
/// Grants the Last Stand ability. While below half health the user can trigger a short adrenaline
/// surge that multiplies their gun damage and adds a flat weapon accuracy bonus.
/// Attach via a job's roundComponents or an AddComponentSpecial; numbers are tuned through
/// <see cref="CMULastStandPrototype"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMULastStandSystem))]
public sealed partial class CMULastStandComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId ActionId = "CMUActionLastStand";

    [DataField, AutoNetworkedField]
    public EntityUid? Action;

    /// <summary>
    /// The tuning prototype holding the damage multiplier, accuracy bonus, duration and cooldowns.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<CMULastStandPrototype> Settings = "CMUDefaultLastStand";
}
