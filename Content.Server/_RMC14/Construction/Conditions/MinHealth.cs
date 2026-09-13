using Content.Server.Destructible;
using Content.Shared.Construction;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;

namespace Content.Server._RMC14.Construction.Conditions;

[DataDefinition]
public sealed partial class MinHealth : IGraphCondition
{
    [DataField]
    public FixedPoint2 Threshold = 1;

    [DataField]
    public bool ByProportion = false;

    [DataField]
    public bool IncludeEquals = true;

    public bool Condition(EntityUid uid, IEntityManager entMan)
    {
        if (!entMan.TryGetComponent(uid, out DestructibleComponent? destructibleComp) ||
            !entMan.TryGetComponent(uid, out DamageableComponent? damageComp))
        {
            return false;
        }

        var destructionSys = entMan.System<DestructibleSystem>();
        var maxHealth = destructionSys.DestroyedAt(uid, destructibleComp);
        if (maxHealth == FixedPoint2.MaxValue || maxHealth <= FixedPoint2.Zero)
            return false;

        var damageSys = entMan.System<DamageableSystem>();
        var curHealth = maxHealth - damageSys.GetTotalDamage((uid, damageComp));
        var health = ByProportion ? curHealth / maxHealth : curHealth;

        if (IncludeEquals)
            return health >= Threshold;

        return health > Threshold;
    }

    public bool DoExamine(ExaminedEvent args)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        var entity = args.Examined;

        if (Condition(entity, entMan))
            return false;

        args.PushMarkup(Loc.GetString("construction-examine-condition-low-health"));
        return true;
    }

    public IEnumerable<ConstructionGuideEntry> GenerateGuideEntry()
    {
        yield return new ConstructionGuideEntry
        {
            Localization = "construction-step-condition-low-health"
        };
    }
}
