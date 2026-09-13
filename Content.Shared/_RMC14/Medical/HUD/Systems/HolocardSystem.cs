using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Overwatch;
using Content.Shared._RMC14.Medical.HUD.Components;
using Content.Shared._RMC14.Medical.HUD.Events;
using Content.Shared._RMC14.Medical.Scanner;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Verbs;
using Robust.Shared.Network;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._RMC14.Medical.HUD.Systems;

public sealed partial class HolocardSystem : EntitySystem
{
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public const int MinimumRequiredSkill = 2;
    public static readonly EntProtoId<SkillDefinitionComponent> SkillType = "RMCSkillMedical";

    public override void Initialize()
    {
        InitializeSourceOwnership();
        SubscribeLocalEvent<HolocardStateComponent, HolocardChangeEvent>(ChangeHolocard);
        SubscribeLocalEvent<HolocardStateComponent, GetVerbsEvent<ExamineVerb>>(OnHolocardExaminableVerb);

        SubscribeLocalEvent<HealthScannerComponent, OpenChangeHolocardUIEvent>(OpenChangeHolocardUI);
        SubscribeLocalEvent<HealthScannerComponent, RefreshEquipmentHudEvent<HealthScannerComponent>>(OnRefreshEquipmentHud);

        SubscribeLocalEvent<HolocardContainerComponent, HolocardContainerStatusUpdateEvent>(OnHolocardContainerStatusUpdate);
        SubscribeLocalEvent<HolocardContainerComponent, EntInsertedIntoContainerMessage>(OnHolocardContainerEntInserted);
        SubscribeLocalEvent<HolocardContainerComponent, EntRemovedFromContainerMessage>(OnHolocardContainerEntRemoved);
    }

    private void ChangeHolocard(Entity<HolocardStateComponent> ent, ref HolocardChangeEvent args)
    {
        if (_net.IsClient || args.UiKey is not HolocardChangeUIKey.Key ||
            !Enum.IsDefined(args.NewHolocardStatus) ||
            !_ui.IsUiOpen(ent.Owner, HolocardChangeUIKey.Key, args.Actor))
            return;

        // Actor comes from the authenticated BUI envelope. Owner is retained on
        // the wire for the existing client, but cannot nominate another medic.
        if (!TryGetEntity(args.Owner, out var viewer) || viewer != args.Actor ||
            !CanChangeHolocard(ent.Owner, args.Actor))
            return;

        ent.Comp.ManualStatus = args.NewHolocardStatus;
        RefreshEffectiveStatus(ent);
    }

    private void OnHolocardExaminableVerb(Entity<HolocardStateComponent> entity, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract)
            return;

        // A player with insufficient medical skill cannot change holocards
        if (!_skills.HasSkill(args.User, SkillType, MinimumRequiredSkill))
            return;

        var scanEvent = new HolocardScanEvent(false, SlotFlags.EYES | SlotFlags.HEAD);
        RaiseLocalEvent(args.User, ref scanEvent);
        if (!scanEvent.CanScan)
            return;

        var target = args.Target;
        var user = args.User;
        var verb = new ExamineVerb()
        {
            Act = () =>
            {
                if (CanChangeHolocard(target, user))
                    _ui.OpenUi(target, HolocardChangeUIKey.Key, user);
            },
            Text = Loc.GetString("scannable-holocard-verb-text"),
            Message = Loc.GetString("scannable-holocard-verb-message"),
            Category = VerbCategory.Examine,
            Icon = new SpriteSpecifier.Texture(new("/Textures/_RMC14/Interface/VerbIcons/ambulance.png")),
        };

        args.Verbs.Add(verb);
    }

    private void OpenChangeHolocardUI(EntityUid entity, HealthScannerComponent comp, ref OpenChangeHolocardUIEvent args)
    {
        if (_net.IsClient || args.UiKey is not HealthScannerUIKey.Key ||
            !_ui.IsUiOpen(entity, HealthScannerUIKey.Key, args.Actor) ||
            !TryGetEntity(args.Owner, out var claimed) || claimed != args.Actor ||
            !TryGetEntity(args.Target, out var target) || comp.Target != target ||
            target is not { } patient || !CanChangeHolocard(patient, args.Actor))
            return;
        _ui.OpenUi(patient, HolocardChangeUIKey.Key, args.Actor);
    }

    private bool CanChangeHolocard(EntityUid patient, EntityUid actor)
    {
        if (TerminatingOrDeleted(patient) || TerminatingOrDeleted(actor) ||
            EntityManager.IsQueuedForDeletion(patient) || EntityManager.IsQueuedForDeletion(actor) ||
            !HasComp<HolocardStateComponent>(patient) ||
            !_skills.HasSkill(actor, SkillType, MinimumRequiredSkill))
            return false;
        return _transform.InRange(patient, actor, 15f) ||
            TryComp<OverwatchWatchingComponent>(actor, out var watching) && watching.Watching == patient;
    }

    private void OnRefreshEquipmentHud(Entity<HealthScannerComponent> ent, ref RefreshEquipmentHudEvent<HealthScannerComponent> args)
    {
        args.Active = true;
    }

    private void OnHolocardContainerStatusUpdate(Entity<HolocardContainerComponent> container, ref HolocardContainerStatusUpdateEvent args)
    {
        _appearance.SetData(container, HolocardContainerVisuals.State, args.NewStatus);
    }

    private void OnHolocardContainerEntInserted(Entity<HolocardContainerComponent> container, ref EntInsertedIntoContainerMessage args)
    {
        var state = HolocardStatus.None;

        if (TryComp<HolocardStateComponent>(args.Entity, out var holocard))
            state = holocard.HolocardStatus;

        _appearance.SetData(container, HolocardContainerVisuals.State, state);
    }

    private void OnHolocardContainerEntRemoved(Entity<HolocardContainerComponent> container, ref EntRemovedFromContainerMessage args)
    {
        _appearance.SetData(container, HolocardContainerVisuals.State, HolocardStatus.None);
    }
}
