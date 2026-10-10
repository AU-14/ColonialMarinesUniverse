using Content.Shared._RMC14.Medical.CPR;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared.Atmos.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Foldable;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Medical.CPR;

public sealed partial class CMUCPRMaskSystem : EntitySystem
{
    [Dependency] private FoldableSystem _foldable = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private RMCUnrevivableSystem _unrevivable = default!;

    private const string MaskSlot = "mask";
    private static readonly ProtoId<TagPrototype> GasMaskTag = "GasMask";

    public override void Initialize()
    {
        SubscribeLocalEvent<InventoryComponent, ReceiveCPRAttemptEvent>(OnReceiveCPRAttempt);

        SubscribeLocalEvent<CMUCPRMaskComponent, BeingEquippedAttemptEvent>(OnMaskBeingEquipped);
        SubscribeLocalEvent<CMUCPRMaskComponent, GotEquippedEvent>(OnMaskEquipped);
        SubscribeLocalEvent<CMUCPRMaskComponent, GotUnequippedEvent>(OnMaskUnequipped);
        SubscribeLocalEvent<CMUCPRMaskWearerComponent, MobStateChangedEvent>(OnWearerMobStateChanged);
    }

    private void OnReceiveCPRAttempt(Entity<InventoryComponent> ent, ref ReceiveCPRAttemptEvent args)
    {
        if (args.Cancelled ||
            !_inventory.TryGetSlotEntity(ent, MaskSlot, out var mask, ent.Comp) ||
            IsMouthClear(mask.Value))
        {
            return;
        }

        args.Cancelled = true;

        if (_net.IsClient)
            return;

        var foldable = HasComp<FoldableComponent>(mask) || HasComp<MaskComponent>(mask);
        var popup = Loc.GetString(foldable ? "cmu-cpr-mask-pull-down" : "cmu-cpr-mask-remove",
            ("target", ent.Owner),
            ("mask", mask.Value));
        _popup.PopupEntity(popup, ent, args.Performer, PopupType.MediumCaution);
    }

    private bool IsMouthClear(EntityUid mask)
    {
        if (HasComp<CMUCPRMaskComponent>(mask) ||
            !HasComp<BreathToolComponent>(mask) && !_tag.HasTag(mask, GasMaskTag))
        {
            return true;
        }

        if (TryComp<FoldableComponent>(mask, out var foldable) && _foldable.IsFolded(mask, foldable))
            return true;

        return TryComp<MaskComponent>(mask, out var toggle) && toggle.IsToggled;
    }

    public void AddMaskBonus(EntityUid target, TimeSpan cprTime)
    {
        if (!_inventory.TryGetSlotEntity(target, MaskSlot, out var mask) ||
            !TryComp<CMUCPRMaskComponent>(mask, out var cprMask))
        {
            return;
        }

        _unrevivable.AddRevivableTime(target, cprTime * cprMask.RevivableTimeBonus);
    }

    private void OnMaskBeingEquipped(Entity<CMUCPRMaskComponent> ent, ref BeingEquippedAttemptEvent args)
    {
        if (!_mobState.IsAlive(args.EquipTarget))
            return;

        args.Reason = "cmu-cpr-mask-conscious";
        args.Cancel();
    }

    private void OnMaskEquipped(Entity<CMUCPRMaskComponent> ent, ref GotEquippedEvent args)
    {
        if (_net.IsClient)
            return;

        EnsureComp<CMUCPRMaskWearerComponent>(args.EquipTarget).Mask = ent;
    }

    private void OnMaskUnequipped(Entity<CMUCPRMaskComponent> ent, ref GotUnequippedEvent args)
    {
        if (_net.IsClient)
            return;

        if (TryComp<CMUCPRMaskWearerComponent>(args.EquipTarget, out var wearer) && wearer.Mask == ent.Owner)
            RemCompDeferred<CMUCPRMaskWearerComponent>(args.EquipTarget);
    }

    private void OnWearerMobStateChanged(Entity<CMUCPRMaskWearerComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive || _net.IsClient)
            return;

        if (!_inventory.TryGetSlotEntity(ent, MaskSlot, out var mask) || !HasComp<CMUCPRMaskComponent>(mask))
            return;

        if (_inventory.TryUnequip(ent, MaskSlot, silent: true, force: true))
            _popup.PopupEntity(Loc.GetString("cmu-cpr-mask-falls-off", ("mask", mask.Value)), ent, PopupType.Small);
    }
}
