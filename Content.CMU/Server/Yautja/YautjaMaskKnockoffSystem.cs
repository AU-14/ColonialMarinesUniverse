using Content.Server.Chat.Systems;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Pheromones;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Yautja;

/// <summary>
/// CM-SS13 parity: a Xenomorph claw attack aimed at the head can smash a Yautja's mask off.
/// Reference: code/modules/mob/living/carbon/xenomorph/attack_alien.dm:127-150. The original roll
/// only runs for a Yautja gas mask worn in the mask slot on a head-aimed attack, happens before the
/// damage is dealt, and on success spends the swing (no damage) while dropping the mask on the
/// floor and making the wearer roar or scream.
/// </summary>
public sealed partial class YautjaMaskKnockoffSystem : EntitySystem
{
    private const string MaskSlot = "mask";

    // attack_alien.dm:129-141. knock_chance starts at 1, frenzy and intelligent castes add to it,
    // the dealt damage adds min(floor(damage * 0.25), 10), and the total is capped at 15% for a
    // wearer with TRAIT_YAUTJA_TECH (all Yautja) or 20% for anyone else. An incapacitated wearer
    // is a flat uncapped 75% instead.
    private const float BaseChance = 1f;
    private const float FrenzyChancePerLevel = 2f;
    private const float IntelligentCasteChance = 2f;
    private const float DamageChanceFactor = 0.25f;
    private const float DamageChanceCap = 10f;
    private const float IncapacitatedChance = 75f;
    private const float YautjaChanceCap = 15f;
    private const float OtherChanceCap = 20f;

    // attack_alien.dm:132 tests caste.is_intelligent, which CM-SS13 sets on King (castes/King.dm:5),
    // Queen (castes/Queen.dm:24) and Predalien (castes/Predalien.dm:23). This fork has no King
    // caste, so only the Queen and the abomination (its predalien) are listed.
    private static readonly EntProtoId<XenoComponent>[] IntelligentCastes =
        ["CMXenoQueen", "CMUXenoAbomination"];

    // CM-SS13 plays "alien_claw_metal" here, which is three numbered variants it picks between; the
    // collection mirrors those files (Content.CMU/Resources/Audio/CMU14/Yautja/alien_claw_metal*.ogg).
    private static readonly SoundSpecifier KnockoffSound = new SoundCollectionSpecifier("CMUAlienClawMetal");

    // attack_alien.dm:148-149: the wearer roars if it is a Yautja and screams otherwise.
    private static readonly ProtoId<EmotePrototype> YautjaRoarEmote = "CMUYautjaRoar";
    private static readonly ProtoId<EmotePrototype> ScreamEmote = "Scream";

    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBodyZoneTargetingSystem _zones = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        // XenoSystem already subscribes XenoComponent+MeleeHitEvent, and the event bus keeps a single
        // handler per (component, event) pair, so this hooks the ubiquitous TransformComponent the way
        // YautjaChainGauntletSystem does for CMDisarmEvent and checks the attacker itself.
        SubscribeLocalEvent<TransformComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(Entity<TransformComponent> ent, ref MeleeHitEvent args)
    {
        if (args.Handled || !args.IsHit)
            return;

        // CM-SS13's roll lives in /mob/living/carbon/human/attack_alien, so only Xenomorph claws count.
        var xeno = args.User;
        if (!HasComp<XenoComponent>(xeno))
            return;

        // attack_alien.dm:127 only rolls when the attack is aimed at the head.
        if (_zones.TryGetSelection(xeno) != TargetBodyZone.Head)
            return;

        var damage = (args.BaseDamage + args.BonusDamage).GetTotal().Float();

        foreach (var target in args.HitEntities)
        {
            // attack_alien.dm:127-128: something must be worn in the mask slot and it must be a
            // Yautja mask; helmets and other headgear are never affected.
            if (!_inventory.TryGetSlotEntity(target, MaskSlot, out var mask) ||
                !HasComp<YautjaMaskComponent>(mask))
            {
                continue;
            }

            if (!_random.Prob(GetKnockoffChance(xeno, target, damage) / 100f))
                continue;

            SmashOffMask(xeno, target, mask.Value);

            // attack_alien.dm:147 returns XENO_ATTACK_ACTION, so the damage of this swing is skipped.
            args.Handled = true;
            return;
        }
    }

    /// <summary>
    /// The CM-SS13 knock_chance formula (attack_alien.dm:129-141), returned as a percentage.
    /// </summary>
    private float GetKnockoffChance(EntityUid xeno, EntityUid target, float damage)
    {
        var chance = BaseChance;

        if (TryComp(xeno, out XenoFrenzyPheromonesComponent? frenzy))
        {
            // The frenzy pheromone component is [Access]-restricted, so read the field and work on the copy.
            var frenzyLevel = frenzy.Multiplier;
            if (frenzyLevel > 0f)
                chance += FrenzyChancePerLevel * frenzyLevel.Float();
        }

        if (IsIntelligentCaste(xeno))
            chance += IntelligentCasteChance;

        chance += MathF.Min(MathF.Floor(damage * DamageChanceFactor), DamageChanceCap);

        // CM-SS13's `if(stat)`: a downed wearer cannot keep the mask on and is rolled at 75% flat.
        if (_mobState.IsIncapacitated(target))
            return IncapacitatedChance;

        // TRAIT_YAUTJA_TECH is inherent to the Yautja species in CM-SS13, so the trained cap applies
        // to them and the untrained cap applies to anyone else wearing a Yautja mask.
        return HasComp<YautjaComponent>(target)
            ? MathF.Min(chance, YautjaChanceCap)
            : MathF.Min(chance, OtherChanceCap);
    }

    private bool IsIntelligentCaste(EntityUid xeno)
    {
        var prototype = MetaData(xeno).EntityPrototype?.ID;
        if (prototype == null)
            return false;

        foreach (var caste in IntelligentCastes)
        {
            if (prototype == caste.Id)
                return true;
        }

        return false;
    }

    private void SmashOffMask(EntityUid xeno, EntityUid target, EntityUid mask)
    {
        _audio.PlayPvs(KnockoffSound, target);

        // attack_alien.dm:144-145: the third person line goes to everyone but the attacker, which
        // gets the "We smash off" line instead.
        var others = Loc.GetString("cmu-yautja-mask-knockoff-others",
            ("xeno", xeno),
            ("target", target),
            ("mask", mask));
        var self = Loc.GetString("cmu-yautja-mask-knockoff-self",
            ("target", target),
            ("mask", mask));

        foreach (var session in Filter.Pvs(target).Recipients)
        {
            if (session.AttachedEntity is not { } recipient)
                continue;

            var text = recipient == xeno ? self : others;
            _popup.PopupEntity(text, target, recipient, PopupType.MediumCaution);
        }

        // attack_alien.dm:146 drop_inv_item_on_ground(wear_mask): TryUnequip drops the item beside
        // the wearer, keeping it a normal item rather than deleting or hiding it.
        if (!_inventory.TryUnequip(target, MaskSlot, out var removedMask, silent: true, force: true))
            return;

        _chat.TryEmoteWithChat(target, HasComp<YautjaComponent>(target) ? YautjaRoarEmote : ScreamEmote);

        // CM-SS13 does not touch the dropped item; nothing else to do with it here.
        _ = removedMask;
    }
}
