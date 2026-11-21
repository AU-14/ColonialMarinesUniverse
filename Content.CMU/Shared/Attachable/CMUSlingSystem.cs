using Content.Shared.Clothing.Components;
using Content.Shared._RMC14.Attachable.Systems;
using Content.Shared.CMU14.Inventory;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Containers;

namespace Content.Shared.CMU14.Attachable;

public sealed partial class CMUSlingSystem : EntitySystem
{
    [Dependency] private AttachableHolderSystem _attachments = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUSlingComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<CMUSlingComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ItemComponent, InteractUsingEvent>(OnVestInteractUsing);
        SubscribeLocalEvent<CMUSlingVestComponent, GetVerbsEvent<AlternativeVerb>>(OnVestGetVerbs);
    }

    // nothing ever called TryRetrieveFromVest, so a rifle clipped to the vest was stuck there for good
    private void OnVestGetVerbs(Entity<CMUSlingVestComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract ||
            !_containers.TryGetContainer(ent, CMUSlingVestComponent.ContainerId, out var container) ||
            container.Count == 0)
        {
            return;
        }

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-sling-vest-retrieve"),
            Priority = 2,
            Act = () => TryRetrieveFromVest(ent, user),
        });
    }

    private void OnGetVerbs(Entity<CMUSlingComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString($"cmu-sling-switch-{MountName(NextMount(ent.Comp.Mount))}"),
            Act = () => ToggleMount(ent, user),
        });
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString($"cmu-sling-switch-carry-{CarryModeName(NextCarryMode(ent.Comp.CarryMode))}"),
            Act = () => ToggleCarryMode(ent, user),
        });
    }

    public void ToggleMount(Entity<CMUSlingComponent> ent, EntityUid user)
    {
        if (_attachments.TryGetHolder(ent, out _))
        {
            _popup.PopupEntity(Loc.GetString("cmu-sling-detach-first"), ent, user);
            return;
        }

        ent.Comp.Mount = NextMount(ent.Comp.Mount);
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString($"cmu-sling-set-{MountName(ent.Comp.Mount)}"),
            ent, user);
    }

    public void ToggleCarryMode(Entity<CMUSlingComponent> ent, EntityUid user)
    {
        ent.Comp.CarryMode = NextCarryMode(ent.Comp.CarryMode);
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString($"cmu-sling-set-carry-{CarryModeName(ent.Comp.CarryMode)}"),
            ent, user);
    }

    private void OnExamined(Entity<CMUSlingComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString($"cmu-sling-examine-{MountName(ent.Comp.Mount)}"));
        args.PushMarkup(Loc.GetString($"cmu-sling-examine-carry-{CarryModeName(ent.Comp.CarryMode)}"));
    }

    private void OnVestInteractUsing(Entity<ItemComponent> vest, ref InteractUsingEvent args)
    {
        if (args.Handled ||
            !HasComp<ClothingComponent>(vest.Owner) ||
            !_inventory.TryGetSlotEntity(args.User, "outerClothing", out var worn) ||
            worn != vest.Owner ||
            !_attachments.HasCMUSlingCarryMode(args.Used, CMUSlingCarryMode.Vest))
        {
            return;
        }

        args.Handled = true;
        TryAttachToVest(vest.Owner, args.Used, args.User);
    }

    public bool TryAttachToVest(EntityUid vest, EntityUid rifle, EntityUid user)
    {
        if (!_attachments.HasCMUSlingCarryMode(rifle, CMUSlingCarryMode.Vest))
            return false;

        EnsureComp<CMUSlingVestComponent>(vest);
        var container = _containers.EnsureContainer<ContainerSlot>(vest, CMUSlingVestComponent.ContainerId);
        container.OccludesLight = false;
        if (container.Count > 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-sling-vest-occupied"), vest, user);
            return false;
        }

        return _containers.Insert(rifle, container);
    }

    public bool TryRetrieveFromVest(EntityUid vest, EntityUid user)
    {
        if (!TryComp(vest, out CMUSlingVestComponent? holder) ||
            holder == null ||
            !_containers.TryGetContainer(vest, CMUSlingVestComponent.ContainerId, out var container) ||
            container.Count == 0)
        {
            return false;
        }

        var rifle = container.ContainedEntities[0];
        if (!_containers.Remove(rifle, container))
            return false;

        _hands.PickupOrDrop(user, rifle);
        return true;
    }

    private static CMUSlingMount NextMount(CMUSlingMount mount) => mount switch
    {
        CMUSlingMount.Sling => CMUSlingMount.Rail,
        CMUSlingMount.Rail => CMUSlingMount.Underbarrel,
        CMUSlingMount.Underbarrel => CMUSlingMount.Sling,
        _ => throw new ArgumentOutOfRangeException(nameof(mount), mount, null),
    };

    private static CMUSlingCarryMode NextCarryMode(CMUSlingCarryMode mode) => mode switch
    {
        CMUSlingCarryMode.SuitStorage => CMUSlingCarryMode.Vest,
        CMUSlingCarryMode.Vest => CMUSlingCarryMode.SuitStorage,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    private static string MountName(CMUSlingMount mount) => mount switch
    {
        CMUSlingMount.Sling => "sling",
        CMUSlingMount.Rail => "rail",
        CMUSlingMount.Underbarrel => "underbarrel",
        _ => throw new ArgumentOutOfRangeException(nameof(mount), mount, null),
    };

    private static string CarryModeName(CMUSlingCarryMode mode) => mode switch
    {
        CMUSlingCarryMode.SuitStorage => "suitstorage",
        CMUSlingCarryMode.Vest => "vest",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };
}
