using Content.Shared.Damage;
using Content.Shared.CMU14.Xenos.Despoiler; // CMU14
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._RMC14.Xenonids.Despoiler;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class XenoDespoilerComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool NextAbilityEmpowered;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan EmpowerExpiresAt;

    [DataField]
    public List<DamageSpecifier> FinishingStabBonusByTier = new();

    // CMU14: each application selects its actual damage and duration tier.
    // [DataField]
    // public ComponentRegistry AcidComponents = new();
    [DataField]
    public List<CMULingeringAcidData> AcidTiers = new();
}
