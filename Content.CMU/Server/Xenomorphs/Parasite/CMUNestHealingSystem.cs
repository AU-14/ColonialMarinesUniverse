using Content.Shared._RMC14.Xenonids.Construction.Nest;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Xenomorphs.Parasite;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Xenomorphs.Parasite;

/// <summary>
///     Nests heal living human hosts at the rate and health cap configured in YAML.
/// </summary>
public sealed partial class CMUNestHealingSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private IGameTiming _timing = default!;

    private TimeSpan _nextHeal;

    public static readonly ProtoId<CMUNestHealingPrototype> HealingSettings = "CMUNestHealing";

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextHeal)
            return;

        _nextHeal = _timing.CurTime + TimeSpan.FromSeconds(1);
        var settings = _prototypes.Index(HealingSettings);

        var query = EntityQueryEnumerator<XenoNestedComponent, CMUHumanMedicalComponent, DamageableComponent>();
        while (query.MoveNext(out var uid, out var nested, out _, out var damageable))
        {
            if (nested.Detached ||
                _mobState.IsDead(uid) ||
                !_thresholds.TryGetDeadThreshold(uid, out var threshold) ||
                threshold <= FixedPoint2.Zero)
            {
                continue;
            }

            var excess = _damage.GetTotalDamage((uid, damageable)) - threshold.Value * (1 - settings.MaxHealthFraction);
            if (excess <= FixedPoint2.Zero)
                continue;

            _damage.HealEvenly((uid, damageable), -FixedPoint2.Min(settings.HealPerSecond, excess));
        }
    }
}
