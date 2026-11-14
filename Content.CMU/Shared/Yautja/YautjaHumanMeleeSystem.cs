using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Random.Helpers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Yautja;

public sealed partial class YautjaHumanMeleeSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPainShockSystem _pain = default!;
    [Dependency] private SkillsSystem _skills = default!;

    private static readonly EntProtoId<SkillDefinitionComponent> MeleeSkill = "RMCSkillMeleeWeapons";

    public override void Initialize()
    {
        // the skill undo and the pen both go on from the victim's side: the hit zone (and so which armour piece got
        // hit) is only known here, CMGetArmorPiercingEvent just gets the target, and a Bad Blood's wide swing can
        // catch humans and non-humans at once, so the undo has to be decided per target
        SubscribeLocalEvent<HumanoidProfileComponent, DamageModifyEvent>(OnHumanDamageModify,
            before: [typeof(CMArmorSystem)]);
        SubscribeLocalEvent<PainShockComponent, DamageChangedEvent>(OnPainedDamageChanged);
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

        // SkillsSystem already multiplied the swing; humans don't get the melee skill bonus, so take it back off.
        // only for melee, thrown hits never got the bonus
        if (args.Impact.Delivery == DamageImpactDelivery.Melee && args.Origin is { } user)
        {
            var skill = _skills.GetSkill(user, MeleeSkill);
            if (skill > 0)
                args.Damage *= 1f / (1f + 0.25f * skill);
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

    private void OnPainedDamageChanged(Entity<PainShockComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased ||
            args.DamageDelta is not { } delta ||
            args.Tool is not { } tool ||
            !TryComp<YautjaHumanMeleeComponent>(tool, out var melee) ||
            !IsHuman(ent))
        {
            return;
        }

        // armour already took its cut, so this is what actually landed
        var landed = FixedPoint2.Zero;
        foreach (var amount in delta.DamageDict.Values)
        {
            if (amount > FixedPoint2.Zero)
                landed += amount;
        }

        _pain.AddPainPulse(ent, landed * melee.PainPerDamage);
    }

    private bool IsHuman(EntityUid target)
    {
        return HasComp<HumanoidProfileComponent>(target) && !HasComp<XenoComponent>(target);
    }
}
