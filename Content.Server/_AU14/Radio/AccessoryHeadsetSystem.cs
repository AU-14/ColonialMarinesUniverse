using Content.Server.Radio.Components;
using Content.Shared._RMC14.UniformAccessories;
using Content.Shared.AU14.Radio;
using Content.Shared.Clothing;
using Content.Shared.Inventory;
using Content.Shared.Radio;
using Robust.Shared.Containers;
using Content.Server._AU14.Marines.Roles.Ranks;
using Content.Shared._AU14.Marines.Roles.Ranks;
using Content.Server._RMC14.Marines.Roles.Ranks;

namespace Content.Server._AU14.Radio;

/// <summary>
///     Grants intrinsic radio to the wearer when an <see cref="AccessoryHeadsetComponent"/>
///     uniform accessory is inside a worn uniform. Mirrors the RadioImplantSystem pattern.
///     Also provides a default radio channel for the :h prefix via <see cref="AccessoryRadioWearerComponent"/>.
/// </summary>
public sealed partial class AccessoryHeadsetSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private RankChangerSystem _rankChanger = default!;
    [Dependency] private RankSystem _rank = default!;

    public override void Initialize()
    {
        base.Initialize();

        // When the earpiece itself is inserted into / removed from a uniform's accessory container
        SubscribeLocalEvent<AccessoryHeadsetComponent, EntGotInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<AccessoryHeadsetComponent, EntGotRemovedFromContainerMessage>(OnRemoved);

        // When the uniform itself is equipped / unequipped while containing an earpiece
        SubscribeLocalEvent<UniformAccessoryHolderComponent, ClothingGotEquippedEvent>(OnHolderEquipped);
        SubscribeLocalEvent<UniformAccessoryHolderComponent, ClothingGotUnequippedEvent>(OnHolderUnequipped);

        // Handle default radio channel for :h prefix
        SubscribeLocalEvent<AccessoryRadioWearerComponent, GetDefaultRadioChannelEvent>(OnGetDefaultRadioChannel);
    }

    /// <summary>
    ///     Provides the default radio channel when the wearer uses the :h prefix.
    /// </summary>
    private void OnGetDefaultRadioChannel(EntityUid uid, AccessoryRadioWearerComponent component, GetDefaultRadioChannelEvent args)
    {
        args.Channel ??= component.DefaultChannel;
    }

    /// <summary>
    ///     Earpiece inserted into a uniform accessory container. If the uniform is currently worn, grant radio.
    /// </summary>
    private void OnInserted(Entity<AccessoryHeadsetComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        var uniform = args.Container.Owner;

        if (!TryComp<UniformAccessoryHolderComponent>(uniform, out var holder) ||
            args.Container.ID != holder.ContainerId)
        {
            return;
        }

        if (TryGetWearer(uniform, out var wearer))
            GrantRadio(ent, wearer);
    }

    /// <summary>
    ///     Earpiece removed from a uniform container. Remove radio from the wearer.
    /// </summary>
    private void OnRemoved(Entity<AccessoryHeadsetComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        RevokeRadio(ent);
    }

    /// <summary>
    ///     Uniform equipped. Check if it contains any earpiece accessories and grant radio.
    /// </summary>
    private void OnHolderEquipped(Entity<UniformAccessoryHolderComponent> ent, ref ClothingGotEquippedEvent args)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.ContainerId, out var container))
            return;

        foreach (var accessory in container.ContainedEntities)
        {
            if (TryComp<AccessoryHeadsetComponent>(accessory, out var headset))
                GrantRadio((accessory, headset), args.Wearer);

            if (TryComp<RankChangerComponent>(accessory, out var changer))
                _rankChanger.ApplyRank(args.Wearer, changer);
        }
    }

    /// <summary>
    ///     Uniform unequipped. Revoke radio from any earpiece accessories inside it.
    /// </summary>
    private void OnHolderUnequipped(Entity<UniformAccessoryHolderComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.ContainerId, out var container))
            return;

        foreach (var accessory in container.ContainedEntities)
        {
            if (TryComp<AccessoryHeadsetComponent>(accessory, out var headset))
                RevokeRadio((accessory, headset));

            if (TryComp<RankChangerComponent>(accessory, out var changer))
                _rankChanger.RevertRank(args.Wearer, changer);
        }
    }

    /// <summary>
    ///     Tries to find the entity wearing a garment via the inventory system.
    /// </summary>
    private bool TryGetWearer(EntityUid uniform, out EntityUid wearer)
    {
        wearer = default;

        // earpieces clip to anything with an accessory holder, not just jumpsuits - vests,
        // coats, webbing and armour all take the Utility category. resolve the wearer from
        // whichever clothing slot the garment is actually in, or clipping the earpiece to
        // a vest silently leaves the wearer off the net
        if (!_container.TryGetContainingContainer((uniform, null, null), out var container))
            return false;

        if (!_inventory.TryGetContainingSlot((uniform, null, null), out var slot))
            return false;

        if ((slot.SlotFlags & (SlotFlags.INNERCLOTHING | SlotFlags.OUTERCLOTHING)) == SlotFlags.NONE)
            return false;

        wearer = container.Owner;
        return wearer.IsValid();
    }

    /// <summary>
    ///     Grants intrinsic radio to the target entity. Mirrors RadioImplantSystem.OnImplantImplanted.
    ///     Also grants <see cref="AccessoryRadioWearerComponent"/> to provide the :h default channel.
    /// </summary>
    private void GrantRadio(Entity<AccessoryHeadsetComponent> ent, EntityUid target)
    {
        // Don't double-grant
        if (ent.Comp.RadioGrantedTo == target)
            return;

        // If previously granted to someone else, revoke first
        if (ent.Comp.RadioGrantedTo != null)
            RevokeRadio(ent);

        ent.Comp.RadioGrantedTo = target;

        var activeRadio = EnsureComp<ActiveRadioComponent>(target);
        foreach (var channel in ent.Comp.Channels)
        {
            if (activeRadio.Channels.Add(channel))
                ent.Comp.ActiveAddedChannels.Add(channel);
        }

        EnsureComp<IntrinsicRadioReceiverComponent>(target);

        var transmitter = EnsureComp<IntrinsicRadioTransmitterComponent>(target);
        foreach (var channel in ent.Comp.Channels)
        {
            if (transmitter.Channels.Add(channel))
                ent.Comp.TransmitterAddedChannels.Add(channel);
        }

        // Grant default channel for the :h prefix
        var defaultChannel = ent.Comp.DefaultChannel?.Id;
        if (defaultChannel == null)
        {
            // Fall back to the first channel if no default is specified
            foreach (var ch in ent.Comp.Channels)
            {
                defaultChannel = ch;
                break;
            }
        }

        if (defaultChannel != null)
        {
            var wearerComp = EnsureComp<AccessoryRadioWearerComponent>(target);
            wearerComp.DefaultChannel ??= defaultChannel;
            Dirty(target, wearerComp);
        }

        Dirty(ent);
    }

    /// <summary>
    ///     Revokes intrinsic radio from whoever it was granted to. Mirrors RadioImplantSystem.OnRemove.
    /// </summary>
    private void RevokeRadio(Entity<AccessoryHeadsetComponent> ent)
    {
        var target = ent.Comp.RadioGrantedTo;
        if (target == null || !Exists(target.Value))
        {
            ClearTracking(ent);
            return;
        }

        if (TryComp<ActiveRadioComponent>(target.Value, out var activeRadio))
        {
            foreach (var channel in ent.Comp.ActiveAddedChannels)
            {
                activeRadio.Channels.Remove(channel);
            }

            if (activeRadio.Channels.Count == 0)
                RemCompDeferred<ActiveRadioComponent>(target.Value);
        }

        if (TryComp<IntrinsicRadioTransmitterComponent>(target.Value, out var transmitter))
        {
            foreach (var channel in ent.Comp.TransmitterAddedChannels)
            {
                transmitter.Channels.Remove(channel);
            }

            if (transmitter.Channels.Count == 0)
                RemCompDeferred<IntrinsicRadioTransmitterComponent>(target.Value);
        }

        // Only remove receiver if there are no active channels left
        if (activeRadio?.Channels.Count == 0)
            RemCompDeferred<IntrinsicRadioReceiverComponent>(target.Value);

        // Remove default channel component
        RemCompDeferred<AccessoryRadioWearerComponent>(target.Value);

        ClearTracking(ent);
    }

    private void ClearTracking(Entity<AccessoryHeadsetComponent> ent)
    {
        ent.Comp.RadioGrantedTo = null;
        ent.Comp.ActiveAddedChannels.Clear();
        ent.Comp.TransmitterAddedChannels.Clear();
        Dirty(ent);
    }
}

