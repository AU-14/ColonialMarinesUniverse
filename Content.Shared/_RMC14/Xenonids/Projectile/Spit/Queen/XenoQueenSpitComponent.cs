using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Xenonids.Projectile.Spit.Queen;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoSpitSystem))]
public sealed partial class XenoQueenSpitComponent : Component
{
    [DataField, AutoNetworkedField]
    public FixedPoint2 PlasmaCost = 40;

    [DataField, AutoNetworkedField]
    public float Speed = 40;

    [DataField, AutoNetworkedField]
    public EntProtoId ProjectileId = "XenoQueenSpitProjectile";

    [DataField, AutoNetworkedField]
    public SoundSpecifier Sound = new SoundCollectionSpecifier("XenoSpitAcid", AudioParams.Default.WithVolume(-5f));

    [DataField, AutoNetworkedField]
    public int MaxProjectiles = 3;

    [DataField, AutoNetworkedField]
    public Angle MaxDeviation = Angle.FromDegrees(15);

    [DataField, AutoNetworkedField]
    public int ProjectileHitLimit = 1;
}
