using Content.Shared._CMU14.Paper;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._CMU14.Paper;

public sealed partial class UniversalPaperToolSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<UniversalPaperToolComponent>(UniversalPaperToolUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<UniversalPaperToolPrintMessage>(OnPrint);
        });

        SubscribeLocalEvent<UniversalPaperToolComponent, EntInsertedIntoContainerMessage>(OnPaperSlotChanged);
        SubscribeLocalEvent<UniversalPaperToolComponent, EntRemovedFromContainerMessage>(OnPaperSlotChanged);
    }

    private void OnUiOpened(Entity<UniversalPaperToolComponent> ent, ref BoundUIOpenedEvent args)
    {
        RefreshUi(ent);
    }

    private void OnPaperSlotChanged<T>(Entity<UniversalPaperToolComponent> ent, ref T args) where T : ContainerModifiedMessage
    {
        RefreshUi(ent);
    }

    private void OnPrint(Entity<UniversalPaperToolComponent> ent, ref UniversalPaperToolPrintMessage msg)
    {
        UniversalPaperToolTemplate? selected = null;
        foreach (var template in ent.Comp.Templates)
        {
            if (template.Prototype.Id != msg.Prototype)
                continue;

            selected = template;
            break;
        }

        if (selected is null)
            return;

        if (!_prototype.TryIndex<EntityPrototype>(selected.Prototype, out var prototype))
            return;
        
        if (!HasComp<PaperComponent>(selected.Prototype))
        {
            _popup.PopupEntity(Loc.GetString("cmu-universal-paper-tool-invalid-template"), ent.Owner, msg.Actor, PopupType.SmallCaution);
            return;
        }

        if (!_itemSlots.TryEject(ent.Owner, UniversalPaperToolComponent.PaperSlotId, msg.Actor, out var paper, excludeUserAudio: true))
        {
            _popup.PopupEntity(Loc.GetString("cmu-universal-paper-tool-no-paper"), ent.Owner, msg.Actor, PopupType.SmallCaution);
            RefreshUi(ent);
            return;
        }

        QueueDel(paper.Value);

        var printed = Spawn(selected.Prototype, Transform(ent.Owner).Coordinates);
        _transform.PlaceNextTo(printed, ent.Owner);
        _audio.PlayPvs(ent.Comp.PrintSound, ent);
        _hands.TryPickupAnyHand(msg.Actor, printed);
        _popup.PopupEntity(
            Loc.GetString("cmu-universal-paper-tool-printed", ("paper", prototype.Name)),
            ent.Owner,
            msg.Actor);
        RefreshUi(ent);
    }

    private void RefreshUi(Entity<UniversalPaperToolComponent> ent)
    {
        var entries = new List<UniversalPaperToolTemplateEntry>(ent.Comp.Templates.Count);
        var hasPaper = _itemSlots.GetItemOrNull(ent.Owner, UniversalPaperToolComponent.PaperSlotId) != null;

        foreach (var template in ent.Comp.Templates)
        {
            if (!_prototype.TryIndex<EntityPrototype>(template.Prototype, out var prototype))
                continue;

            entries.Add(new UniversalPaperToolTemplateEntry(
                template.Prototype.Id,
                template.Name ?? prototype.Name,
                template.Description ?? prototype.Description));
        }

        _ui.SetUiState(ent.Owner, UniversalPaperToolUiKey.Key, new UniversalPaperToolBuiState(entries, hasPaper));
    }
}
