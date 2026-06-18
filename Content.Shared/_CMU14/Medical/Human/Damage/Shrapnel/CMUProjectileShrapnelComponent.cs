using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Human.Damage.Shrapnel;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUProjectileShrapnelComponent : Component
{
    [DataField]
    public int Fragments = 1;

    [DataField]
    public float Severity = 10f;

    [DataField]
    public FixedPoint2 MoveDamage;

    [DataField]
    public FixedPoint2 MoveDamagePerFragment = FixedPoint2.New(0.5);

    [DataField]
    public bool CanExplode;

    [DataField]
    public float ExplosionChance;

    [DataField]
    public ForeignObjectDepth RemovalDepth = ForeignObjectDepth.Surface;
}
