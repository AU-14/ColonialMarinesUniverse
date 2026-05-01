using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.CrawlState;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedCrawlWhileCritSystem))]
public sealed partial class CrawlWhileCritComponent : Component
{
    [DataField]
    public FixedPoint2 CrawlWindow = 30;

    [DataField]
    public TimeSpan ActivationDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public float WalkSpeedModifier = 0.15f;

    [DataField]
    public float SprintSpeedModifier = 0.15f;

    [DataField]
    public List<ProtoId<DamageGroupPrototype>> AbortOnDamageGroups = new() { "Brute", "Burn" };
}
