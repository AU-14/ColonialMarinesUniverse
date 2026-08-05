using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;

namespace Content.Shared._RMC14.HealthExaminable;

public sealed partial class RMCHealthExaminableSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;

    private static readonly FixedPoint2[] Thresholds = new FixedPoint2[] { 25, 50, 75, 100, 200, 300 };

    public override void Initialize()
    {
        SubscribeLocalEvent<RMCHealthExaminableComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<RMCHealthExaminableComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.SpeciesType == null)
            return;

        if (!TryComp(ent, out DamageableComponent? damageable))
            return;

        var damagePerGroup = _damageable.GetDamagePerGroup((ent, damageable));
        using (args.PushGroup(nameof(RMCHealthExaminableSystem), -1))
        {
            foreach (var group in ent.Comp.Groups)
            {
                if (!damagePerGroup.TryGetValue(group, out var groupDamage))
                    continue;

                for (var i = Thresholds.Length - 1; i >= 0; i--)
                {
                    var threshold = Thresholds[i];
                    if (groupDamage < threshold)
                        continue;

                    var id = $"rmc-health-examinable-{ent.Comp.SpeciesType}-{group}-{threshold.Int()}";
                    if (!Loc.TryGetString(id, out var msg, ("target", Identity.Entity(ent, EntityManager, args.Examiner))))
                        continue;

                    args.PushMarkup(msg);
                    break;
                }
            }
        }
    }
}
