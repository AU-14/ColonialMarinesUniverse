using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Xenomorphs.Pathogen.Mycotoxin;
using Content.Shared.CMU14.Xenomorphs.Pathogen.Sporecaster;
using Content.Shared.CMU14.Xenomorphs.Pathogen.SporeSac;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Shared.CombatMode;
using Content.Shared.Clothing.Components;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Xenomorphs.Pathogen;

[Serializable, NetSerializable]
public sealed partial class CMUNeoRemoveHeadgearEvent : SimpleDoAfterEvent;

public sealed partial class CMUNeoHeadgearSystem : EntitySystem
{
    [Dependency] private SharedBodyZoneTargetingSystem _zones = default!;
    [Dependency] private SharedCombatModeSystem _combat = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedChatSystem _chat = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;

    private const string MaskSlot = "mask";

    private static readonly ProtoId<EmotePrototype> ScreamEmote = "Scream";

    private static readonly SoundSpecifier KnockoffSound = new SoundCollectionSpecifier("CMUAlienClawMetal");

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUPathogenHiveMemberComponent, CMUNeoRemoveHeadgearEvent>(OnFinished);
    }

    public bool TryStart(EntityUid neo, EntityUid target)
    {
        if (!HasComp<CMUPathogenHiveMemberComponent>(neo) || !CanStrip(neo, target) ||
            !TryGetGear(target, out var item, out _))
            return false;

        return _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, neo, 2f,
            new CMUNeoRemoveHeadgearEvent(), neo, target, item)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        });
    }

    private bool CanStrip(EntityUid neo, EntityUid target)
    {
        if (neo == target || _combat.IsInCombatMode(neo) || _zones.TryGetSelection(neo) != TargetBodyZone.Head ||
            !_interaction.InRangeUnobstructed(neo, target))
            return false;

        var position = Transform(neo).Coordinates;
        return Nearby<CMUPathogenSporecasterComponent>(position) || Nearby<CMUPathogenSporeSacComponent>(position);
    }

    private bool Nearby<T>(EntityCoordinates position) where T : Component
    {
        foreach (var source in _lookup.GetEntitiesInRange<T>(position, 6f))
        {
            if (position.InRange(EntityManager, Transform(source.Owner).Coordinates, 6f))
                return true;
        }
        return false;
    }

    private bool TryGetGear(EntityUid target, out EntityUid? gear, out string slot)
    {
        slot = "mask";
        if (IsRemovableGear(target, slot, out gear))
            return true;
        slot = "head";
        return IsRemovableGear(target, slot, out gear);
    }

    private bool IsRemovableGear(EntityUid target, string slot, out EntityUid? gear) =>
        _inventory.TryGetSlotEntity(target, slot, out gear) && HasComp<ClothingComponent>(gear) && !HasComp<XenoParasiteComponent>(gear);

    /// <summary>
    /// Rolls the chance and, on success, rips the first spore-protective mask off the target
    /// onto the ground. Only masks carrying <see cref="MycotoxinProtectionComponent"/> are
    /// targeted: the rip exists to open the spore war, not to strip fashion.
    /// </summary>
    public bool TryRipMask(EntityUid xeno, EntityUid target, float chance)
    {
        if (!_random.Prob(chance))
            return false;

        if (!_inventory.TryGetSlotEntity(target, MaskSlot, out var mask))
            return false;

        if (!HasComp<MycotoxinProtectionComponent>(mask))
            return false;

        _audio.PlayPvs(KnockoffSound, target);

        var others = Loc.GetString("cmu-pathogen-mask-rip-others",
            ("xeno", xeno),
            ("target", target),
            ("mask", mask.Value));
        var self = Loc.GetString("cmu-pathogen-mask-rip-self",
            ("target", target),
            ("mask", mask.Value));

        _popup.PopupEntity(self, xeno, xeno, PopupType.MediumCaution);
        _popup.PopupEntity(others, target,
            Filter.Pvs(target).RemoveWhereAttachedEntity(o => o == xeno),
            true, PopupType.MediumCaution);

        if (!_inventory.TryUnequip(target, MaskSlot, out _, silent: true, force: true))
            return false;

        _chat.TryEmoteWithChat(target, ScreamEmote);
        return true;
    }

    private void OnFinished(Entity<CMUPathogenHiveMemberComponent> neo, ref CMUNeoRemoveHeadgearEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target || !CanStrip(neo, target) ||
            !TryGetGear(target, out var current, out var slot) || current != args.Used)
            return;

        args.Handled = true;
        if (_net.IsServer)
            _inventory.TryUnequip(neo, target, slot, force: true);
    }
}
