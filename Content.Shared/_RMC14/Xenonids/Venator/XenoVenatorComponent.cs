using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._RMC14.Xenonids.Venator;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoVenatorSystem))]
public sealed partial class XenoVenatorComponent : Component
{
    [DataField, AutoNetworkedField]
    public int AcidCharges;

    [DataField, AutoNetworkedField]
    public int MaxAcidCharges = 5;

    [DataField, AutoNetworkedField]
    public int ArmorPenaltyPerCharge = 5;

    [DataField, AutoNetworkedField]
    public float DamageTakenMultiplierPerAcidCharge = 0.05f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan StoreAcidLockedUntil;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoVenatorSystem))]
public sealed partial class XenoVenatorPoolOnHitComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId Pool = "XenoAcidSprayWeak";

    [DataField, AutoNetworkedField]
    public int Rings;

    [DataField, AutoNetworkedField]
    public bool IgnoreDirectTarget = true;

    [DataField, AutoNetworkedField]
    public bool UpgradeDirectHit;

    [DataField, AutoNetworkedField]
    public bool RandomCrossPattern;

    public bool Pooled;
}
