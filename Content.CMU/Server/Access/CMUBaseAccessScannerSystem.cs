using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.GameTicking;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.CMU14.Access;
using Content.Shared.Database;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Access;

public sealed class CMUBaseAccessScannerSystem : EntitySystem
{
    private static readonly ProtoId<AccessLevelPrototype> SecurityAccess = "AU14AccessGovforSecurity";
    private const int VisibleEntries = 12;

    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedIdCardSystem _idCards = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUBaseAccessScannerComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<CMUBaseAccessScannerComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CMUBaseAccessScannerComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<CMUBaseAccessScannerComponent, BoundUIClosedEvent>(OnUiClosed);
        SubscribeLocalEvent<CMUBaseAccessScannerComponent, EntRemovedFromContainerMessage>(OnCardRemoved);
        SubscribeLocalEvent<CMUBaseAccessScannerComponent, CMUBaseAccessInsertOperatorCardBuiMsg>(OnInsertCard);
        SubscribeLocalEvent<CMUBaseAccessScannerComponent, CMUBaseAccessEjectOperatorCardBuiMsg>(OnEjectCard);
        SubscribeLocalEvent<CMUBaseAccessScannerComponent, CMUBaseAccessSetDirectionBuiMsg>(OnSetDirection);
    }

    private void OnActivate(EntityUid uid, CMUBaseAccessScannerComponent comp, ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex || !_ui.HasUi(uid, CMUBaseAccessScannerUi.Key))
            return;

        args.Handled = true;
        if (!TryClaim(uid, comp, args.User))
            return;

        _ui.TryOpenUi(uid, CMUBaseAccessScannerUi.Key, args.User);
        RefreshUi(uid, comp);
    }

    private void OnInteractUsing(EntityUid uid, CMUBaseAccessScannerComponent comp, InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<IdCardComponent>(args.Used, out var card))
            return;

        args.Handled = true;
        if (GetOperatorCard(uid) == null)
        {
            if (!TryInsertOperatorCard(uid, comp, args.Used, card, args.User))
                return;

            _ui.TryOpenUi(uid, CMUBaseAccessScannerUi.Key, args.User);
            RefreshUi(uid, comp);
            return;
        }

        if (!TryGetAuthorizedOperator(uid, out _, out var operatorCard))
        {
            _popup.PopupEntity(Loc.GetString("cmu-base-access-scanner-no-operator"), uid, args.User);
            return;
        }

        RecordScan(uid, comp, card, operatorCard, args.User);
    }

    private void RecordScan(
        EntityUid uid,
        CMUBaseAccessScannerComponent comp,
        IdCardComponent scanned,
        IdCardComponent approving,
        EntityUid user)
    {
        var name = scanned.FullName ?? Loc.GetString("cmu-base-access-scanner-unknown-name");
        var rank = string.IsNullOrWhiteSpace(scanned.LocalizedJobTitle)
            ? Loc.GetString("cmu-base-access-scanner-unknown-rank")
            : scanned.LocalizedJobTitle;
        var approver = approving.FullName ?? Loc.GetString("cmu-base-access-scanner-unknown-name");

        comp.Entries.Add(new CMUBaseAccessLogEntry(_gameTicker.RoundDuration(), name, rank, comp.Direction, approver));
        while (comp.Entries.Count > comp.MaxEntries)
            comp.Entries.RemoveAt(0);

        comp.StatusMessage = Loc.GetString("cmu-base-access-scanner-recorded",
            ("name", name),
            ("direction", DirectionText(comp.Direction)));

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(user):user} used {ToPrettyString(uid):scanner} as {approver} to record {name} ({rank}) {comp.Direction} Joint Base Schaefer");
        _popup.PopupEntity(comp.StatusMessage, uid, user);
        RefreshUi(uid, comp);
    }

    private void OnInsertCard(EntityUid uid, CMUBaseAccessScannerComponent comp, CMUBaseAccessInsertOperatorCardBuiMsg args)
    {
        if (args.Actor != comp.CurrentUser || GetOperatorCard(uid) != null)
            return;

        if (!TryFindUsersCard(args.Actor, out var cardUid) || !TryComp<IdCardComponent>(cardUid, out var card))
        {
            _popup.PopupEntity(Loc.GetString("cmu-base-access-scanner-no-id"), uid, args.Actor);
            return;
        }

        if (TryInsertOperatorCard(uid, comp, cardUid, card, args.Actor))
            RefreshUi(uid, comp);
    }

    private void OnEjectCard(EntityUid uid, CMUBaseAccessScannerComponent comp, CMUBaseAccessEjectOperatorCardBuiMsg args)
    {
        if (args.Actor != comp.CurrentUser)
            return;

        EjectOperatorCard(uid, comp, args.Actor);
    }

    private void OnSetDirection(EntityUid uid, CMUBaseAccessScannerComponent comp, CMUBaseAccessSetDirectionBuiMsg args)
    {
        if (args.Actor != comp.CurrentUser || !TryGetAuthorizedOperator(uid, out _, out _))
            return;

        comp.Direction = args.Direction;
        comp.StatusMessage = Loc.GetString("cmu-base-access-scanner-mode-set",
            ("direction", DirectionText(comp.Direction)));
        RefreshUi(uid, comp);
    }

    private bool TryInsertOperatorCard(
        EntityUid uid,
        CMUBaseAccessScannerComponent comp,
        EntityUid cardUid,
        IdCardComponent card,
        EntityUid user)
    {
        if (!IsSecurityCard(cardUid))
        {
            _popup.PopupEntity(Loc.GetString("cmu-base-access-scanner-access-denied"), uid, user);
            return false;
        }

        if (!TryClaim(uid, comp, user) || GetOperatorCard(uid) != null)
            return false;

        var slot = _container.EnsureContainer<ContainerSlot>(uid, CMUBaseAccessScannerComponent.OperatorCardSlotId);
        if (!_container.Insert(cardUid, slot))
            return false;

        comp.CardInsertedBy = user;
        comp.CardInsertedAt = _timing.CurTime;
        comp.StatusMessage = Loc.GetString("cmu-base-access-scanner-operator-authorized",
            ("name", card.FullName ?? Loc.GetString("cmu-base-access-scanner-unknown-name")));
        _audio.PlayPvs(comp.InsertSound, uid);
        return true;
    }

    private void EjectOperatorCard(EntityUid uid, CMUBaseAccessScannerComponent comp, EntityUid user)
    {
        if (GetOperatorCard(uid) is not { } card ||
            !_container.TryGetContainer(uid, CMUBaseAccessScannerComponent.OperatorCardSlotId, out var slot) ||
            !_container.Remove(card, slot))
            return;

        _hands.PickupOrDrop(user, card);
        _audio.PlayPvs(comp.EjectSound, uid);
    }

    private void OnCardRemoved(EntityUid uid, CMUBaseAccessScannerComponent comp, EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != CMUBaseAccessScannerComponent.OperatorCardSlotId || TerminatingOrDeleted(uid))
            return;

        comp.CardInsertedBy = null;
        comp.CardInsertedAt = null;
        comp.StatusMessage = Loc.GetString("cmu-base-access-scanner-insert-operator");
        RefreshUi(uid, comp);
    }

    private void OnUiOpened(EntityUid uid, CMUBaseAccessScannerComponent comp, BoundUIOpenedEvent args)
    {
        TryClaim(uid, comp, args.Actor);
        RefreshUi(uid, comp);
    }

    private void OnUiClosed(EntityUid uid, CMUBaseAccessScannerComponent comp, BoundUIClosedEvent args)
    {
        if (comp.CurrentUser == args.Actor)
            comp.CurrentUser = null;
    }

    private bool TryClaim(EntityUid uid, CMUBaseAccessScannerComponent comp, EntityUid user)
    {
        if (comp.CurrentUser is { } current && current != user &&
            _ui.IsUiOpen(uid, CMUBaseAccessScannerUi.Key, current))
        {
            _popup.PopupEntity(Loc.GetString("cmu-base-access-scanner-in-use"), uid, user);
            return false;
        }

        comp.CurrentUser = user;
        return true;
    }

    private EntityUid? GetOperatorCard(EntityUid uid)
    {
        return _container.TryGetContainer(uid, CMUBaseAccessScannerComponent.OperatorCardSlotId, out var slot) &&
               slot is ContainerSlot cardSlot
            ? cardSlot.ContainedEntity
            : null;
    }

    private bool TryGetAuthorizedOperator(EntityUid uid, out EntityUid cardUid, out IdCardComponent card)
    {
        cardUid = default;
        card = default!;
        if (GetOperatorCard(uid) is not { } inserted ||
            !TryComp<IdCardComponent>(inserted, out var insertedCard) ||
            !IsSecurityCard(inserted))
            return false;

        cardUid = inserted;
        card = insertedCard;
        return true;
    }

    private bool IsSecurityCard(EntityUid card)
    {
        return TryComp<AccessComponent>(card, out var access) &&
               access.Enabled &&
               access.Tags.Contains(SecurityAccess);
    }

    private bool TryFindUsersCard(EntityUid user, out EntityUid card)
    {
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (!HasComp<IdCardComponent>(held))
                continue;

            card = held;
            return true;
        }

        card = default;
        if (!_idCards.TryFindIdCard(user, out var worn))
            return false;

        card = worn.Owner;
        return true;
    }

    private void RefreshUi(EntityUid uid, CMUBaseAccessScannerComponent comp)
    {
        var authorized = TryGetAuthorizedOperator(uid, out var cardUid, out var card);
        var cardPrototype = cardUid.IsValid() ? MetaData(cardUid).EntityPrototype?.ID : null;
        var state = new CMUBaseAccessScannerBuiState(
            GetOperatorCard(uid) != null,
            comp.CardInsertedAt,
            cardPrototype,
            authorized,
            authorized ? card.FullName ?? Loc.GetString("cmu-base-access-scanner-unknown-name") : string.Empty,
            authorized ? card.LocalizedJobTitle ?? string.Empty : string.Empty,
            comp.Direction,
            comp.StatusMessage,
            comp.Entries.AsEnumerable().Reverse().Take(VisibleEntries).ToArray());

        _ui.SetUiState(uid, CMUBaseAccessScannerUi.Key, state);
    }

    private static string DirectionText(CMUBaseAccessDirection direction)
        => direction == CMUBaseAccessDirection.Entering ? "entering" : "leaving";
}
