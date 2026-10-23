using Content.Client._RMC14.Attachable.Components;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Attachable.Events;
using Content.Shared._RMC14.Attachable.Systems;
using Content.Shared.CMU14.Attachable; // CMU14
using Robust.Client.GameObjects;
using Robust.Shared.Containers;

namespace Content.Client._RMC14.Attachable.Systems;

public sealed partial class AttachableHolderVisualsSystem : EntitySystem
{
    [Dependency] private AttachableHolderSystem _attachableHolderSystem = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AttachableHolderVisualsComponent, EntRemovedFromContainerMessage>(OnDetached);
        SubscribeLocalEvent<AttachableHolderVisualsComponent, AttachableHolderAttachablesAlteredEvent>(OnAttachablesAltered);

        SubscribeLocalEvent<AttachableVisualsComponent, AppearanceChangeEvent>(OnAttachableAppearanceChange);
    }

    private void OnDetached(Entity<AttachableHolderVisualsComponent> holder, ref EntRemovedFromContainerMessage args)
    {
        if (!HasComp<AttachableVisualsComponent>(args.Entity) || !_attachableHolderSystem.HasSlot(holder.Owner, args.Container.ID))
            return;

        var holderEv = new AttachableHolderAttachablesAlteredEvent(args.Entity, args.Container.ID, AttachableAlteredType.Detached);
        RaiseLocalEvent(holder, ref holderEv);
    }

    private void OnAttachablesAltered(Entity<AttachableHolderVisualsComponent> holder,
        ref AttachableHolderAttachablesAlteredEvent args)
    {
        if (!TryComp(args.Attachable, out AttachableVisualsComponent? attachableComponent))
            return;

        string suffix = "";
        if (attachableComponent.ShowActive && TryComp(args.Attachable, out AttachableToggleableComponent? toggleableComponent) && toggleableComponent.Active)
            suffix = "-on";

        var attachable = new Entity<AttachableVisualsComponent>(args.Attachable, attachableComponent);
        switch (args.Alteration)
        {
            case AttachableAlteredType.Attached:
                SetAttachableOverlay(holder, attachable, args.SlotId, suffix);
                break;

            case AttachableAlteredType.Detached:
                RemoveAttachableOverlay(holder, args.SlotId);
                break;

            case AttachableAlteredType.Activated:
                if (!attachableComponent.ShowActive)
                    break;

                SetAttachableOverlay(holder, attachable, args.SlotId, suffix);
                break;

            case AttachableAlteredType.Deactivated:
                if (!attachableComponent.ShowActive)
                    break;

                SetAttachableOverlay(holder, attachable, args.SlotId, suffix);
                break;

            case AttachableAlteredType.Interrupted:
                if (!attachableComponent.ShowActive)
                    break;

                SetAttachableOverlay(holder, attachable, args.SlotId);
                break;

            case AttachableAlteredType.AppearanceChanged:
                SetAttachableOverlay(holder, attachable, args.SlotId, suffix);
                break;
        }
    }

    private void RemoveAttachableOverlay(Entity<AttachableHolderVisualsComponent> holder, string slotId)
    {
        if (!TryComp(holder, out SpriteComponent? spriteComponent)) // CMU14: dynamically added sling mounts also have overlays.
            return;

        if (!_sprite.LayerMapTryGet((holder.Owner, spriteComponent), slotId, out var index, false))
            return;

        _sprite.LayerMapRemove((holder.Owner, spriteComponent), slotId, out _);
        _sprite.RemoveLayer((holder.Owner, spriteComponent), index);
    }

    private void SetAttachableOverlay(Entity<AttachableHolderVisualsComponent> holder,
        Entity<AttachableVisualsComponent> attachable,
        string slotId,
        string suffix = "")
    {
        RefreshVisuals(holder, attachable, slotId, suffix);
    }

    private void OnAttachableAppearanceChange(Entity<AttachableVisualsComponent> attachable, ref AppearanceChangeEvent args)
    {
        if (!attachable.Comp.RedrawOnAppearanceChange ||
            !_attachableHolderSystem.TryGetHolder(attachable.Owner, out var holderUid) ||
            !_attachableHolderSystem.TryGetSlotId(holderUid.Value, attachable.Owner, out var slotId))
        {
            return;
        }

        var holderEvent = new AttachableHolderAttachablesAlteredEvent(attachable.Owner, slotId, AttachableAlteredType.AppearanceChanged);
        RaiseLocalEvent(holderUid.Value, ref holderEvent);
    }

    // cmu edit start: the attachment offset tool edits offsets live and previews attachments that aren't fitted.
    /// <summary>
    ///     Moves a slot's attachment overlay on this client only, adding the slot to the offsets if it wasn't drawn
    ///     before, and redraws whatever is in it. A server update to the holder puts the real offsets back.
    /// </summary>
    public void CMUSetOffset(Entity<AttachableHolderVisualsComponent> holder, string slotId, System.Numerics.Vector2 offset)
    {
        holder.Comp.Offsets[slotId] = offset;
        if (TryComp(holder, out AttachableHolderComponent? holderComp) &&
            _attachableHolderSystem.TryGetAttachable((holder.Owner, holderComp), slotId, out var attached) &&
            TryComp(attached.Owner, out AttachableVisualsComponent? visuals))
        {
            RefreshVisuals(holder, (attached.Owner, visuals), slotId, visuals.LastSuffix ?? "");
        }
    }

    /// <summary>
    ///     Draws a client-side copy of an attachment in a slot, for lining it up. Clear with <see cref="CMUClearPreview"/>.
    /// </summary>
    public bool CMUPreview(Entity<AttachableHolderVisualsComponent> holder, string slotId, EntityUid preview)
    {
        if (!TryComp(preview, out AttachableVisualsComponent? visuals))
            return false;

        if (!holder.Comp.Offsets.ContainsKey(slotId))
            holder.Comp.Offsets[slotId] = System.Numerics.Vector2.Zero;

        RefreshVisuals(holder, (preview, visuals), slotId, "");
        return true;
    }

    /// <summary>A weapon's slot offsets, in tiles, for reading.</summary>
    public IReadOnlyDictionary<string, System.Numerics.Vector2> CMUGetOffsets(Entity<AttachableHolderVisualsComponent> holder)
    {
        return holder.Comp.Offsets;
    }

    /// <summary>Removes a preview from a slot, putting back the real attachment if there is one.</summary>
    public void CMUClearPreview(Entity<AttachableHolderVisualsComponent> holder, string slotId)
    {
        RemoveAttachableOverlay(holder, slotId);
        if (TryComp(holder, out AttachableHolderComponent? holderComp) &&
            _attachableHolderSystem.TryGetAttachable((holder.Owner, holderComp), slotId, out var attached) &&
            TryComp(attached.Owner, out AttachableVisualsComponent? visuals))
        {
            RefreshVisuals(holder, (attached.Owner, visuals), slotId, visuals.LastSuffix ?? "");
        }
    }
    // cmu edit end

    public void RefreshVisuals(Entity<AttachableHolderVisualsComponent> holder, Entity<AttachableVisualsComponent> attachable, string slotId, string suffix)
    {
        // CMU14: slings keep their rail alignment on both alternate mounts.
        var offsetSlot = HasComp<CMUSlingComponent>(attachable) &&
                         slotId is "rmc-aslot-underbarrel" or AttachableHolderSystem.CMUSlingSlot
            ? "rmc-aslot-rail"
            : slotId;
        if (!holder.Comp.Offsets.TryGetValue(offsetSlot, out var offset))
            return;

        if (!TryComp(holder, out SpriteComponent? holderSprite))
        {
            return;
        }

        if (!TryComp(attachable, out SpriteComponent? attachableSprite))
            return;

        attachable.Comp.LastSlotId = slotId;
        attachable.Comp.LastSuffix = suffix;

        var rsi = _sprite.LayerGetEffectiveRsi((attachable.Owner, attachableSprite), attachable.Comp.Layer)?.Path;
        var state = _sprite.LayerGetRsiState((attachable.Owner, attachableSprite), attachable.Comp.Layer).ToString();
        if (attachable.Comp.Rsi is { } rsiPath)
            rsi = rsiPath;

        if (!string.IsNullOrWhiteSpace(attachable.Comp.Prefix))
            state = attachable.Comp.Prefix;

        if (attachable.Comp.IncludeSlotName)
            state += slotId;

        if (!string.IsNullOrWhiteSpace(attachable.Comp.Suffix))
            state += attachable.Comp.Suffix;

        state += suffix;

        var layerData = new PrototypeLayerData()
        {
            RsiPath = rsi.ToString(),
            State = state,
            Offset = offset + attachable.Comp.Offset, // CMU14: includes the sling's rail alignment.
            Visible = true,
        };

        if (_sprite.LayerMapTryGet((holder.Owner, holderSprite), slotId, out var index, false))
        {
            _sprite.LayerSetData((holder.Owner, holderSprite), index, layerData);
            return;
        }

        _sprite.LayerMapSet((holder.Owner, holderSprite), slotId, _sprite.AddLayer((holder.Owner, holderSprite), layerData, null));
    }
}
