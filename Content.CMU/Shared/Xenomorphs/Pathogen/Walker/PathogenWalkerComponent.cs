using Robust.Shared.GameStates;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.Walker;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUPathogenWalkerComponent : Component
{
    [DataField, AutoNetworkedField]
    public int MaxRevives = 2;

    [DataField, AutoNetworkedField]
    public int RevivesUsed;

    [DataField, AutoNetworkedField]
    public TimeSpan ReviveDelay = TimeSpan.FromSeconds(60);

    [DataField, AutoNetworkedField]
    public EntityUid? Hive;

    [DataField, AutoNetworkedField]
    public TimeSpan? ReviveAt;

    /// <summary>Sickly pale skin tone applied on turning/revive.</summary>
    [DataField, AutoNetworkedField]
    public Color WalkerSkinColor = Color.FromHex("#d6d6ce");

    /// <summary>Glowing eye color applied on turning/revive.</summary>
    [DataField, AutoNetworkedField]
    public Color WalkerEyeColor = Color.FromHex("#8cff6b");

    /// <summary>How much Brute/Burn damage is healed per tick while alive.</summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 HealPerTick = 2;

    [DataField, AutoNetworkedField]
    public TimeSpan HealInterval = TimeSpan.FromSeconds(1);

    [DataField, AutoNetworkedField]
    public TimeSpan NextHeal;

    [DataField, AutoNetworkedField]
    public bool PreReviveJitterPlayed;

    [DataField, AutoNetworkedField]
    public EntProtoId MarkerPrototype = "CMU14ClothingHeadWalkerMarker";

    [DataField, AutoNetworkedField]
    public EntityUid? MarkerItem;
}