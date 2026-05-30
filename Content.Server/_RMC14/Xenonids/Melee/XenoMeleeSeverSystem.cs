using Content.Shared._CMU14.Medical.BodyPart.Events;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Melee;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Random;

namespace Content.Server._RMC14.Xenonids.Melee;

public sealed class XenoMeleeSeverSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private XenoSystem _xeno = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoMeleeSeverComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(Entity<XenoMeleeSeverComponent> xeno, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        foreach (var target in args.HitEntities)
        {
            if (!_xeno.CanAbilityAttackTarget(xeno, target))
                continue;

            if (_mobState.IsDead(target))
                continue;

            if (!TryComp<BodyComponent>(target, out _))
                continue;

            var armorEv = new CMGetArmorEvent(SlotFlags.OUTERCLOTHING);
            RaiseLocalEvent(target, ref armorEv);
            var chance = armorEv.Melee > 0 ? xeno.Comp.ArmoredChance : xeno.Comp.Chance;

            if (!_random.Prob(chance))
                continue;

            TrySeverRandomLimb(target);
        }
    }

    private void TrySeverRandomLimb(EntityUid body)
    {
        var arms = new List<(EntityUid Id, BodyPartComponent Part)>();
        var legs = new List<(EntityUid Id, BodyPartComponent Part)>();

        foreach (var (partUid, part) in _body.GetBodyChildren(body))
        {
            if (part.PartType is BodyPartType.Arm)
                arms.Add((partUid, part));
            else if (part.PartType is BodyPartType.Leg)
                legs.Add((partUid, part));
        }

        if (arms.Count == 0 && legs.Count == 0)
            return;

        List<(EntityUid Id, BodyPartComponent Part)> chosen;

        if (arms.Count > 0 && legs.Count > 0)
            chosen = _random.Prob(0.6f) ? arms : legs;
        else if (arms.Count > 0)
            chosen = arms;
        else
            chosen = legs;

        var (severedPartUid, severedPart) = _random.Pick(chosen);
        var ev = new BodyPartSeveredEvent(body, severedPartUid, severedPart.PartType);
        RaiseLocalEvent(severedPartUid, ref ev);
    }
}
