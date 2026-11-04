using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Random.Helpers;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Yautja;

public sealed partial class YautjaHumanMeleeSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SkillsSystem _skills = default!;

    private static readonly EntProtoId<SkillDefinitionComponent> MeleeSkill = "RMCSkillMeleeWeapons";

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaHumanMeleeComponent, MeleeHitEvent>(OnMeleeHit);
        // the pen goes on from the victim's side because the hit zone (and so which armour piece got hit) is only
        // known here, CMGetArmorPiercingEvent just gets the target
        SubscribeLocalEvent<HumanoidProfileComponent, DamageModifyEvent>(OnHumanDamageModify,
            before: [typeof(CMArmorSystem)]);
    }

    private void OnMeleeHit(Entity<YautjaHumanMeleeComponent> ent, ref MeleeHitEvent args)
    {
        // pred weapons don't wide swing, so the one target decides it
        if (!args.IsHit || args.HitEntities.Count != 1 || !IsHuman(args.HitEntities[0]))
            return;

        // SkillsSystem already multiplied the swing, take it back off
        var skill = _skills.GetSkill(args.User, MeleeSkill);
        if (skill <= 0)
            return;

        var coefficient = 1f / (1f + 0.25f * skill);
        var undo = new DamageModifierSet();
        foreach (var type in args.BaseDamage.DamageDict.Keys)
        {
            undo.Coefficients[type] = coefficient;
        }

        args.ModifiersList.Add(undo);
    }

    private void OnHumanDamageModify(Entity<HumanoidProfileComponent> ent, ref DamageModifyEvent args)
    {
        // thrown hits come through here too, cm gave those the same sharp/edge penetration
        if (args.Tool is not { } tool ||
            !TryComp<YautjaHumanMeleeComponent>(tool, out var melee) ||
            !IsHuman(ent) ||
            !args.Damage.AnyPositive())
        {
            return;
        }

        // cm: prob(getarmor(zone)) and the blade loses its sharp/edge, so the armour on that spot counts in full
        var armor = new CMGetArmorEvent(args.TargetSlots, TargetPart: args.TargetPart, TargetZone: args.TargetZone);
        RaiseLocalEvent(ent, ref armor);
        var meleeArmor = (int) (armor.Melee * armor.ArmorModifier);
        if (meleeArmor > 0 &&
            SharedRandomExtensions.PredictedProb(_timing,
                Math.Min(1f, meleeArmor * melee.StripChancePerArmor),
                GetNetEntity(ent),
                GetNetEntity(tool)))
        {
            return;
        }

        args.ArmorPiercing += melee.Penetration;
    }

    private bool IsHuman(EntityUid target)
    {
        return HasComp<HumanoidProfileComponent>(target) && !HasComp<XenoComponent>(target);
    }
}
