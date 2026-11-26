using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared.Interaction;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;

namespace Content.Shared.CMU14.Dropship.AttachmentPoint;

/// <summary>
/// Lets crew open and load a <see cref="GunshipAmmoRackComponent"/> through the utility point it is installed on.
/// </summary>
public sealed partial class GunshipAmmoRackSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunshipAmmoRackComponent, InteractHandEvent>(OnRackInteractHand);
        SubscribeLocalEvent<GunshipAmmoRackComponent, AccessibleOverrideEvent>(OnRackAccessibleOverride);
        SubscribeLocalEvent<GunshipUtilityAttachmentPointComponent, InteractUsingEvent>(OnPointInteractUsing);
    }

    /// <summary>
    /// Gets the dropship utility point the rack is installed on.
    /// </summary>
    public bool TryGetInstalledPoint(EntityUid rack, out EntityUid point)
    {
        point = default;
        if (!_container.TryGetContainingContainer(rack, out var container)
            || !TryComp(container.Owner, out DropshipUtilityPointComponent? utilityPoint)
            || container.ID != utilityPoint.UtilitySlotId)
        {
            return false;
        }

        point = container.Owner;
        return true;
    }

    // DropshipUtilitySystem forwards empty-hand clicks on the point to the installed utility.
    private void OnRackInteractHand(Entity<GunshipAmmoRackComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled
            || !TryGetInstalledPoint(ent, out _)
            || !TryComp(ent, out StorageComponent? storage))
        {
            return;
        }

        if (_ui.IsUiOpen(ent.Owner, StorageComponent.StorageUiKey.Key, args.User))
            _ui.CloseUi(ent.Owner, StorageComponent.StorageUiKey.Key, args.User);
        else
            _storage.OpenStorageUI(ent, args.User, storage, false);

        args.Handled = true;
    }

    // The installed rack sits inside the point's container, so whoever can reach the point can reach the rack.
    private void OnRackAccessibleOverride(Entity<GunshipAmmoRackComponent> ent, ref AccessibleOverrideEvent args)
    {
        if (args.Accessible
            || args.Target != ent.Owner
            || !TryGetInstalledPoint(ent, out var point)
            || !_interaction.CanAccess(args.User, point))
        {
            return;
        }

        args.Accessible = true;
        args.Handled = true;
    }

    private void OnPointInteractUsing(Entity<GunshipUtilityAttachmentPointComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled
            || !TryComp(ent, out DropshipUtilityPointComponent? utilityPoint)
            || !_container.TryGetContainer(ent, utilityPoint.UtilitySlotId, out var slot)
            || slot.ContainedEntities.Count == 0)
        {
            return;
        }

        var rack = slot.ContainedEntities[0];
        if (!HasComp<GunshipAmmoRackComponent>(rack)
            || !TryComp(rack, out StorageComponent? storage)
            || !_whitelist.IsWhitelistPass(storage.Whitelist, args.Used))
        {
            return;
        }

        _storage.PlayerInsertHeldEntity((rack, storage), args.User);
        args.Handled = true;
    }
}
