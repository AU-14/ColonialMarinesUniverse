using System.Diagnostics.CodeAnalysis;
using Content.Server.Administration.Logs;
using Content.Shared._AU14.Radio;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Robust.Shared.Containers;

namespace Content.Server._AU14.Radio;

public sealed partial class ANPRCCryptoSystem : EntitySystem
{
    public const string FillSlotId = "fill_card";

    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;

    private readonly Dictionary<string, int> _generation = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<ANPRCCryptoSlotComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<ANPRCCryptoSlotComponent, EntInsertedIntoContainerMessage>(OnFillInserted);
        SubscribeLocalEvent<ANPRCCryptoSlotComponent, EntRemovedFromContainerMessage>(OnFillRemoved);
        SubscribeLocalEvent<ANPRCFillCardComponent, MapInitEvent>(OnFillCardMapInit);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        Subs.BuiEvents<ANPRCCryptoSlotComponent>(ANPRCRadioUI.Key, subs =>
        {
            subs.Event<ANPRCCryptoZeroizeMsg>(OnZeroize);
            subs.Event<ANPRCCryptoDestroyMsg>(OnDestroy);
            subs.Event<ANPRCCryptoRecryptoMsg>(OnRecrypto);
        });
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _generation.Clear();
    }

    private void OnFillCardMapInit(Entity<ANPRCFillCardComponent> ent, ref MapInitEvent args)
    {
        if (string.IsNullOrEmpty(ent.Comp.Faction))
            return;

        ent.Comp.Generation = GetGeneration(ent.Comp.Faction);
        Dirty(ent);
    }

    private void OnFillInserted(EntityUid uid, ANPRCCryptoSlotComponent comp, EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != FillSlotId)
            return;

        RaiseLocalEvent(uid, new ANPRCCryptoChangedEvent());
    }

    private void OnFillRemoved(EntityUid uid, ANPRCCryptoSlotComponent comp, EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != FillSlotId)
            return;

        RaiseLocalEvent(uid, new ANPRCCryptoChangedEvent());
    }

    private void OnZeroize(Entity<ANPRCCryptoSlotComponent> ent, ref ANPRCCryptoZeroizeMsg args)
    {
        if (!_itemSlots.TryGetSlot(ent.Owner, FillSlotId, out var slot) || !slot.HasItem)
        {
            _popup.PopupEntity(
                Loc.GetString("anprc-crypto-no-card"),
                args.Actor,
                args.Actor,
                PopupType.SmallCaution);

            return;
        }

        var designation = GetFillDesignation(ent.Owner);
        _itemSlots.TryEjectToHands(ent.Owner, slot, args.Actor);

        _adminLogger.Add(
            LogType.Action,
            LogImpact.Medium,
            $"{ToPrettyString(args.Actor):user} zeroized COMSEC fill {designation} on {ToPrettyString(ent.Owner):radio}");

        _popup.PopupEntity(
            Loc.GetString("anprc-crypto-zeroized", ("designation", designation)),
            args.Actor,
            args.Actor,
            PopupType.Medium);
    }

    private void OnDestroy(Entity<ANPRCCryptoSlotComponent> ent, ref ANPRCCryptoDestroyMsg args)
    {
        if (!_itemSlots.TryGetSlot(ent.Owner, FillSlotId, out var slot) || slot.Item is not { } card)
        {
            _popup.PopupEntity(
                Loc.GetString("anprc-crypto-no-card"),
                args.Actor,
                args.Actor,
                PopupType.SmallCaution);

            return;
        }

        var designation = GetFillDesignation(ent.Owner);
        QueueDel(card);

        _adminLogger.Add(
            LogType.Action,
            LogImpact.High,
            $"{ToPrettyString(args.Actor):user} destroyed COMSEC fill card {designation} in {ToPrettyString(ent.Owner):radio}");

        _popup.PopupEntity(
            Loc.GetString("anprc-crypto-destroyed", ("designation", designation)),
            args.Actor,
            args.Actor,
            PopupType.LargeCaution);
    }

    private void OnRecrypto(Entity<ANPRCCryptoSlotComponent> ent, ref ANPRCCryptoRecryptoMsg args)
    {
        // superseding the faction's fills is a command decision, not an operator one
        if (!HasComp<ANPRCCryptoAuthorityComponent>(args.Actor))
        {
            _popup.PopupEntity(
                Loc.GetString("anprc-recrypto-not-authorized"),
                args.Actor,
                args.Actor,
                PopupType.SmallCaution);

            return;
        }

        if (!TryGetFillCard(ent.Owner, out var card, out var cardUid) || string.IsNullOrEmpty(card.Faction))
        {
            _popup.PopupEntity(
                Loc.GetString("anprc-recrypto-no-card"),
                args.Actor,
                args.Actor,
                PopupType.SmallCaution);

            return;
        }

        if (TryComp(ent.Owner, out ANPRCRadioComponent? radio) &&
            !string.IsNullOrEmpty(radio.OperatorFaction) &&
            !string.Equals(radio.OperatorFaction, card.Faction, StringComparison.OrdinalIgnoreCase))
        {
            _popup.PopupEntity(
                Loc.GetString("anprc-recrypto-foreign-card"),
                args.Actor,
                args.Actor,
                PopupType.SmallCaution);

            return;
        }

        if (card.Generation != GetGeneration(card.Faction))
        {
            _popup.PopupEntity(
                Loc.GetString("anprc-recrypto-stale-card"),
                args.Actor,
                args.Actor,
                PopupType.SmallCaution);

            return;
        }

        var faction = card.Faction;
        var newGeneration = GetGeneration(faction) + 1;

        _generation[faction] = newGeneration;
        card.Generation = newGeneration;

        Dirty(cardUid, card);
        RaiseLocalEvent(ent.Owner, new ANPRCCryptoChangedEvent());

        NotifySupersededRadios(ent.Owner, faction);

        // supersedes every fill card in the faction, admins need to see who pulled
        // the trigger when comms suddenly go unsecured for everyone
        _adminLogger.Add(
            LogType.Action,
            LogImpact.High,
            $"{ToPrettyString(args.Actor):user} ordered a COMSEC recrypto for faction {faction} (generation {newGeneration}), superseding all previously issued fill cards");

        _popup.PopupEntity(
            Loc.GetString("anprc-recrypto-ordered", ("faction", faction)),
            args.Actor,
            args.Actor,
            PopupType.Large);
    }

    // a recrypto silently staled every other card in the faction; without this the
    // other panels keep showing SECURED until something else refreshes them, and
    // operators only find out from COMSEC warnings mid-transmission
    private void NotifySupersededRadios(EntityUid orderingRadio, string faction)
    {
        var radios = EntityQueryEnumerator<ANPRCCryptoSlotComponent>();

        while (radios.MoveNext(out var radioUid, out _))
        {
            if (radioUid == orderingRadio)
                continue;

            if (!string.Equals(GetFillFaction(radioUid), faction, StringComparison.OrdinalIgnoreCase))
                continue;

            RaiseLocalEvent(radioUid, new ANPRCCryptoChangedEvent());

            if (!IsFillStale(radioUid))
                continue;

            if (!TryComp(radioUid, out ANPRCRadioComponent? radio) || !radio.IsEquipped)
                continue;

            var wearer = Transform(radioUid).ParentUid;

            if (!wearer.IsValid())
                continue;

            _popup.PopupEntity(
                Loc.GetString("anprc-recrypto-superseded-notice"),
                wearer,
                wearer,
                PopupType.MediumCaution);
        }
    }

    private void OnExamine(Entity<ANPRCCryptoSlotComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(ANPRCCryptoSlotComponent)))
        {
            var faction = GetFillFaction(ent.Owner);

            if (string.IsNullOrEmpty(faction))
            {
                args.PushMarkup(Loc.GetString("anprc-crypto-examine-empty"));
                return;
            }

            var message = IsFillStale(ent.Owner)
                ? "anprc-crypto-examine-stale"
                : "anprc-crypto-examine-loaded";

            args.PushMarkup(Loc.GetString(
                message,
                ("designation", GetFillDesignation(ent.Owner)),
                ("faction", faction)));
        }
    }

    public bool HasMatchingCrypto(EntityUid anprc, RadioChannelPrototype channel)
    {
        if (!TryGetFillCard(anprc, out var fill, out _))
            return false;

        if (string.IsNullOrEmpty(channel.Faction))
            return true;

        if (fill.Faction != channel.Faction)
            return false;

        return fill.Generation == GetGeneration(fill.Faction);
    }

    public string GetFillFaction(EntityUid anprc)
    {
        return TryGetFillCard(anprc, out var fill, out _)
            ? fill.Faction
            : string.Empty;
    }

    public string GetFillDesignation(EntityUid anprc)
    {
        return TryGetFillCard(anprc, out var fill, out _)
            ? fill.Designation
            : string.Empty;
    }

    public bool IsFillStale(EntityUid anprc)
    {
        return TryGetFillCard(anprc, out var fill, out _) &&
               !string.IsNullOrEmpty(fill.Faction) &&
               fill.Generation != GetGeneration(fill.Faction);
    }

    private int GetGeneration(string faction)
    {
        return _generation.TryGetValue(faction, out var generation)
            ? generation
            : 0;
    }

    private bool TryGetFillCard(
        EntityUid anprc,
        [NotNullWhen(true)] out ANPRCFillCardComponent? fill,
        out EntityUid cardUid)
    {
        fill = null;
        cardUid = default;

        if (!HasComp<ANPRCCryptoSlotComponent>(anprc))
            return false;

        if (!_itemSlots.TryGetSlot(anprc, FillSlotId, out var slot) || slot.Item is not { } card)
            return false;

        cardUid = card;
        return TryComp(card, out fill);
    }
}

public record struct ANPRCCryptoChangedEvent;
