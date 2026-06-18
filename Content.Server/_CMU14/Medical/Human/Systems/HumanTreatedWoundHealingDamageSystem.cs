using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared._RMC14.Damage;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Medical.Human.Systems;

public sealed partial class HumanTreatedWoundHealingDamageSystem : EntitySystem
{
    private static readonly ProtoId<DamageGroupPrototype> BruteDamageGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> BurnDamageGroup = "Burn";

    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedRMCDamageableSystem _rmcDamageable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanTreatedWoundHealingTickEvent>(OnTreatedWoundHealingTick);
    }

    private void OnTreatedWoundHealingTick(ref HumanTreatedWoundHealingTickEvent args)
    {
        if (args.BruteHealed <= FixedPoint2.Zero &&
            args.BurnHealed <= FixedPoint2.Zero)
        {
            return;
        }

        if (!TryComp<DamageableComponent>(args.Body, out var damageable))
            return;

        var heal = new DamageSpecifier();
        if (args.BruteHealed > FixedPoint2.Zero)
        {
            heal = _rmcDamageable.DistributeHealingCached(
                (args.Body, damageable),
                BruteDamageGroup,
                args.BruteHealed,
                heal);
        }

        if (args.BurnHealed > FixedPoint2.Zero)
        {
            heal = _rmcDamageable.DistributeHealingCached(
                (args.Body, damageable),
                BurnDamageGroup,
                args.BurnHealed,
                heal);
        }

        if (heal.GetTotal() >= FixedPoint2.Zero)
            return;

        _damageable.TryChangeDamage(
            args.Body,
            heal,
            ignoreResistances: true,
            interruptsDoAfters: false,
            damageable: damageable);
    }
}
