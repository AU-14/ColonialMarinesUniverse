using Content.Server.Destructible;
using Content.Shared.Construction;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;

namespace Content.Server._RMC14.Construction.Conditions;

/// <summary>
/// Requires that a structure has at least a configured amount of health remaining.
/// </summary>
[DataDefinition]
public sealed partial class RMCMinHealth : IGraphCondition
{
    /// <summary>
    /// When <see cref="ByProportion"/> is true, this is the minimum proportion of health remaining.
    /// Otherwise, this is the minimum absolute health remaining.
    /// </summary>
    [DataField]
    public FixedPoint2 Threshold = 1;

    [DataField]
    public bool ByProportion;

    [DataField]
    public bool IncludeEquals = true;

    public bool Condition(EntityUid uid, IEntityManager entMan)
    {
        if (!entMan.TryGetComponent(uid, out DestructibleComponent? destructible) ||
            !entMan.TryGetComponent(uid, out DamageableComponent? damageable))
        {
            return false;
        }

        var destructibleSystem = entMan.System<DestructibleSystem>();
        var damageableSystem = entMan.System<DamageableSystem>();
        var maxHealth = destructibleSystem.DestroyedAt(uid, destructible);
        var currentHealth = maxHealth - damageableSystem.GetTotalDamage((uid, damageable));
        var value = ByProportion ? currentHealth / maxHealth : currentHealth;

        return IncludeEquals ? value >= Threshold : value > Threshold;
    }

    public bool DoExamine(ExaminedEvent args)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        if (Condition(args.Examined, entMan))
            return false;

        args.PushMarkup(Loc.GetString("construction-examine-condition-low-health"));
        return true;
    }

    public IEnumerable<ConstructionGuideEntry> GenerateGuideEntry()
    {
        yield return new ConstructionGuideEntry
        {
            Localization = "construction-step-condition-low-health",
        };
    }
}
