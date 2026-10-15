using Content.Server.Light.EntitySystems;
using Content.Shared.Actions;
using Content.Shared.CMU14.Light;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Light;
using Content.Shared.Light.Components;
using Content.Shared.Storage;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Light;

/// <summary>
/// Lets the wearer toggle lights stored in the helmet on their head, and turns them off when they leave it.
/// </summary>
public sealed class CMUHelmetMountedLightSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private HandheldLightSystem _handheldLight = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> HelmetTag = "RMCHelmet";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUHelmetMountedLightComponent, EntGotInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<CMUHelmetMountedLightComponent, EntGotRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<CMUHelmetMountedLightComponent, CMUToggleHelmetLightActionEvent>(OnToggleAction);
        SubscribeLocalEvent<CMUHelmetMountedLightComponent, LightToggleEvent>(OnLightToggled);

        SubscribeLocalEvent<CMUHelmetLightHolderComponent, GotEquippedEvent>(OnHelmetEquipped);
        SubscribeLocalEvent<CMUHelmetLightHolderComponent, GotUnequippedEvent>(OnHelmetUnequipped);
    }

    private void OnInserted(Entity<CMUHelmetMountedLightComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == StorageComponent.ContainerId && _tag.HasTag(args.Container.Owner, HelmetTag))
            EnsureComp<CMUHelmetLightHolderComponent>(args.Container.Owner);

        Refresh(ent);
    }

    private void OnRemoved(Entity<CMUHelmetMountedLightComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        Refresh(ent);
    }

    private void OnHelmetEquipped(Entity<CMUHelmetLightHolderComponent> ent, ref GotEquippedEvent args)
    {
        RefreshStoredLights(ent);
    }

    private void OnHelmetUnequipped(Entity<CMUHelmetLightHolderComponent> ent, ref GotUnequippedEvent args)
    {
        RefreshStoredLights(ent);
    }

    private void OnToggleAction(Entity<CMUHelmetMountedLightComponent> ent, ref CMUToggleHelmetLightActionEvent args)
    {
        if (args.Handled || !TryComp(ent, out HandheldLightComponent? light))
            return;

        args.Handled = true;
        _handheldLight.ToggleStatus(args.Performer, (ent, light));
    }

    private void OnLightToggled(Entity<CMUHelmetMountedLightComponent> ent, ref LightToggleEvent args)
    {
        _actions.SetToggled(ent.Comp.ActionEntity, args.IsOn);
    }

    private void RefreshStoredLights(EntityUid helmet)
    {
        if (!_container.TryGetContainer(helmet, StorageComponent.ContainerId, out var storage))
            return;

        foreach (var stored in storage.ContainedEntities)
        {
            if (TryComp(stored, out CMUHelmetMountedLightComponent? light))
                Refresh((stored, light));
        }
    }

    private void Refresh(Entity<CMUHelmetMountedLightComponent> ent)
    {
        var wearer = GetWearer(ent);
        if (wearer == ent.Comp.Wearer)
            return;

        if (ent.Comp.Wearer is { } oldWearer)
        {
            _actions.RemoveAction(oldWearer, ent.Comp.ActionEntity);
            ent.Comp.Wearer = null;

            // It left the worn helmet, so nothing can switch it off any more.
            if (TryComp(ent, out HandheldLightComponent? light) && light.Activated)
                _handheldLight.TurnOff((ent, light));
        }

        if (wearer is not { } newWearer)
            return;

        if (!_actions.AddAction(newWearer, ref ent.Comp.ActionEntity, ent.Comp.Action, ent))
            return;

        ent.Comp.Wearer = newWearer;
        _actions.SetToggled(ent.Comp.ActionEntity, TryComp(ent, out HandheldLightComponent? handheld) && handheld.Activated);
    }

    /// <summary>
    /// Whoever is wearing the helmet this light is stored in, if any.
    /// </summary>
    private EntityUid? GetWearer(EntityUid light)
    {
        if (!_container.TryGetContainingContainer((light, null, null), out var storage) ||
            storage.ID != StorageComponent.ContainerId ||
            !HasComp<CMUHelmetLightHolderComponent>(storage.Owner))
        {
            return null;
        }

        var helmet = storage.Owner;
        if (!_inventory.TryGetContainingSlot((helmet, null, null), out var slot) ||
            (slot.SlotFlags & SlotFlags.HEAD) == 0 ||
            !_container.TryGetContainingContainer((helmet, null, null), out var slotContainer))
        {
            return null;
        }

        return slotContainer.Owner;
    }
}
