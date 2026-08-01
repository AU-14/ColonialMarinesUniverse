using Content.Shared._RMC14.Construction;
using Content.Shared._RMC14.Marines.Skills;

namespace Content.Shared._AU14.Construction;

/// <summary>AU14 construction-skill policy layered over RMC's generic pricing and timing extension events.</summary>
public sealed partial class AU14ConstructionSkillSystem : EntitySystem
{
    [Dependency] private SkillsSystem _skills = default!;

    public const float MaterialDiscountPerSkill = 0.10f;
    private const float SpeedBonusPerSkill = 0.10f;

    private static readonly HashSet<string> DiscountedStacks = new()
    {
        "CMSteel", "CMPlasteel", "RMCWood", "CMRodMetal", "CMRodPlasteel",
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RMCConstructionCostEvent>(OnCost);
        SubscribeLocalEvent<RMCConstructionDelayEvent>(OnDelay);
        SubscribeLocalEvent<RMCConstructionTransactionCompletedEvent>(OnTransactionCompleted);
    }

    private void OnCost(ref RMCConstructionCostEvent args)
    {
        var level = _skills.GetSkill(args.User, "RMCSkillConstruction");
        args.Cost = GetMaterialCost(args.StackType, args.BaseCost, level);
    }

    /// <summary>Returns the authoritative integral material cost used by both construction and its client guide.</summary>
    public static int GetMaterialCost(string stackType, int baseCost, int skillLevel)
    {
        if (skillLevel <= 0 || !DiscountedStacks.Contains(stackType))
            return baseCost;

        var multiplier = MathF.Max(0.1f, 1f - MaterialDiscountPerSkill * skillLevel);
        // Floor so every full skill point produces an actual reduction whenever the recipe costs more than one.
        return Math.Max(1, (int) MathF.Floor(baseCost * multiplier));
    }

    public static int GetDiscountPercent(int skillLevel)
        => Math.Clamp(skillLevel * (int) (MaterialDiscountPerSkill * 100), 0, 90);

    private void OnDelay(ref RMCConstructionDelayEvent args)
    {
        var level = _skills.GetSkill(args.User, "RMCSkillConstruction");
        if (level > 0)
        {
            var multiplier = Math.Max(0.1, 1.0 - SpeedBonusPerSkill * level);
            args.Delay = args.BaseDelay * multiplier;
        }
    }

    private void OnTransactionCompleted(ref RMCConstructionTransactionCompletedEvent args)
    {
        if (args.MaterialShortfall <= 0 || string.IsNullOrEmpty(args.StackType))
            return;

        var shortfall = EnsureComp<AU14MaterialShortfallComponent>(args.Built);
        shortfall.MissingByStack[args.StackType] =
            shortfall.MissingByStack.GetValueOrDefault(args.StackType) + args.MaterialShortfall;
    }
}
