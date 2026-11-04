using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Weapons.Ranged.Backblast;

[RegisterComponent]
public sealed partial class CMUBackblastComponent : Component
{
    [DataField(required: true)]
    public EntProtoId NearEffect;

    [DataField(required: true)]
    public EntProtoId FarEffect;

    [DataField]
    public float KnockbackDistance = 2;

    [DataField]
    public float KnockbackSpeed = 25;

    [DataField]
    public TimeSpan KnockdownTime = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan DeafTime = TimeSpan.FromSeconds(15);

    [DataField]
    public TimeSpan StutterTime = TimeSpan.FromSeconds(15);

    [DataField]
    public TimeSpan DizzyTime = TimeSpan.FromSeconds(2);

    [DataField]
    public int BruteMin = 5;

    [DataField]
    public int BruteMax = 15;

    [DataField]
    public int BurnMin = 5;

    [DataField]
    public int BurnMax = 15;

    [DataField]
    public float FireChance = 0.3f;

    [DataField]
    public int FireIntensity = 15;

    [DataField]
    public int FireDuration = 20;

    [DataField]
    public float ConcussionChance = 0.05f;

    [DataField]
    public FixedPoint2 ConcussionDamage = 16;
}
