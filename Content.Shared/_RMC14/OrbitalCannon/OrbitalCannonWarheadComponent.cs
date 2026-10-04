using Content.Shared.Explosion;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.OrbitalCannon;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(OrbitalCannonSystem))]
public sealed partial class OrbitalCannonWarheadComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId<OrbitalCannonExplosionComponent> Explosion;

    [DataField, AutoNetworkedField]
    public bool IsAegis;

    [DataField, AutoNetworkedField]
    public int FirstWarningRange = 30;

    [DataField, AutoNetworkedField]
    public int SecondWarningRange = 25;

    [DataField, AutoNetworkedField]
    public int ThirdWarningRange = 15;

    /// <summary>
    /// Intel points awarded when this warhead is successfully fired.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 IntelPointsAwarded = FixedPoint2.Zero;

    [DataField, AutoNetworkedField]
    public ProtoId<ExplosionPrototype>? TransitExplosionType;

    [DataField, AutoNetworkedField]
    public float TransitExplosionTotal = 1000;

    [DataField, AutoNetworkedField]
    public float TransitExplosionSlope = 20;

    [DataField, AutoNetworkedField]
    public float TransitExplosionMax = 60;

    [DataField, AutoNetworkedField]
    public float TransitTileBreakScale = 1f;

    [DataField, AutoNetworkedField]
    public int TransitMaxTileBreak = 1;
}
