using System.Linq;
using Content.Server.CMU14.Ops.ThirdParty;
using Content.Server.CMU14.WorldMarket; // RUMC: the world map
using Content.Server.Chat.Systems;
using Content.Server.Popups;
using Content.Server.Radio;
using Content.Server.Radio.EntitySystems;
using Content.Server.Stack;
using Content.Shared.CMU14.Ambassador;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.CMU14.Threats;
using Content.Shared.CMU14.WorldMarket; // RUMC: the world map
using Content.Shared._RMC14.Intel.Tech;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared.Stacks;
using Content.Shared.Interaction;
using Content.Shared.Radio;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using ThirdPartySystem = Content.Server.CMU14.Ops.ThirdParty.ThirdPartySystem;

namespace Content.Server.CMU14.Ambassador;

public sealed partial class AmbassadorConsoleSystem : EntitySystem
{
    private static readonly EntProtoId CashPrototype = "RMCSpaceCash";

    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private ThirdPartySystem _thirdParty = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ColonyEconomy.AdminConsoleSystem _adminConsole = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private WorldMarketSystem _worldMarket = default!; // RUMC: the world map
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan UiRefreshInterval = TimeSpan.FromSeconds(1);

    private readonly HashSet<string> _tickedFactions = new();
    private TimeSpan _nextUiRefresh;

    private static readonly SoundSpecifier MarineAnnouncementSound =
        new SoundPathSpecifier("/Audio/_RMC14/Announcements/Marine/notice2.ogg");

    private static readonly ProtoId<TagPrototype> CurrencyTag = "Currency";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AmbassadorConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<AmbassadorConsoleComponent, AmbassadorWithdrawBuiMsg>(OnWithdraw);
        SubscribeLocalEvent<AmbassadorConsoleComponent, AmbassadorCallThirdPartyBuiMsg>(OnCallThirdParty);
        SubscribeLocalEvent<AmbassadorConsoleComponent, AmbassadorToggleEmbargoBuiMsg>(OnToggleEmbargo);
        SubscribeLocalEvent<AmbassadorConsoleComponent, AmbassadorToggleTradePactBuiMsg>(OnToggleTradePact);
        SubscribeLocalEvent<AmbassadorConsoleComponent, AmbassadorToggleSignalBoostBuiMsg>(OnToggleSignalBoost);
        SubscribeLocalEvent<AmbassadorConsoleComponent, AmbassadorToggleSignalJamBuiMsg>(OnToggleSignalJam);
        SubscribeLocalEvent<AmbassadorConsoleComponent, AmbassadorBroadcastBuiMsg>(OnBroadcast);
        SubscribeLocalEvent<AmbassadorConsoleComponent, AmbassadorToggleCommsJamBuiMsg>(OnToggleCommsJam);
        SubscribeLocalEvent<AmbassadorConsoleComponent, AmbassadorOpenThirdPartyBuiMsg>(OnOpenThirdParty);
        SubscribeLocalEvent<AmbassadorConsoleComponent, AmbassadorScanRadarBuiMsg>(OnScanRadar);
        SubscribeLocalEvent<AmbassadorConsoleComponent, AmbassadorOpenMapBuiMsg>(OnOpenMap); // RUMC: the world map
        SubscribeLocalEvent<AmbassadorConsoleComponent, GiftCityMessage>(OnGiftCity); // RUMC: the world map
        SubscribeLocalEvent<AmbassadorConsoleComponent, CityActionMessage>(OnCityAction); // RUMC: the world map
        SubscribeLocalEvent<AmbassadorConsoleComponent, AmbassadorOpenMotherBuiMsg>(OnOpenMother); // RUMC: the exchange with the mother faction
        SubscribeLocalEvent<AmbassadorConsoleComponent, MotherOrderMessage>(OnMotherOrder); // RUMC: the exchange with the mother faction
        SubscribeLocalEvent<AmbassadorConsoleComponent, MotherSubsidyMessage>(OnMotherSubsidy); // RUMC: the exchange with the mother faction
        SubscribeLocalEvent<AmbassadorConsoleComponent, MotherReportMessage>(OnMotherReport); // RUMC: the exchange with the mother faction
        SubscribeLocalEvent<AmbassadorConsoleComponent, ZoneClaimMessage>(OnZoneClaim); // RUMC: the world map
        SubscribeLocalEvent<AmbassadorConsoleComponent, MapCommandMessage>(OnMapCommand); // RUMC: the world map
        SubscribeLocalEvent<AmbassadorConsoleComponent, EntInsertedIntoContainerMessage>(OnCashInserted);
        SubscribeLocalEvent<AmbassadorConsoleComponent, InteractUsingEvent>(OnInteractUsing);

        SubscribeLocalEvent<RadioSendAttemptEvent>(OnRadioSendAttempt);
    }

    private void OnRadioSendAttempt(ref RadioSendAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (IsCommsJammed())
            args.Cancelled = true;
    }

    // ---- Faction syncing helpers ----

    /// <summary>
    /// Syncs shared state from source to all other consoles with the same faction.
    /// Budget, active statuses, and timers are synced.
    /// </summary>
    private void SyncFaction(AmbassadorConsoleComponent source)
    {
        var query = EntityQueryEnumerator<AmbassadorConsoleComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            if (comp == source || comp.FactionName != source.FactionName)
                continue;

            comp.Budget = source.Budget;
            comp.EmbargoActive = source.EmbargoActive;
            comp.EmbargoTimer = source.EmbargoTimer;
            comp.TradePactActive = source.TradePactActive;
            comp.TradePactTimer = source.TradePactTimer;
            comp.CommsJamActive = source.CommsJamActive;
            comp.CommsJamTimer = source.CommsJamTimer;
            comp.SignalBoostActive = source.SignalBoostActive;
            comp.SignalBoostTimer = source.SignalBoostTimer;
            comp.SignalJamActive = source.SignalJamActive;
            comp.SignalJamTimer = source.SignalJamTimer;
            // this runs every tick, don't reallocate the set unless it actually changed
            if (!comp.CalledParties.SetEquals(source.CalledParties))
                comp.CalledParties = new HashSet<string>(source.CalledParties);
            comp.NextCallInAt = source.NextCallInAt;
        }
    }

    /// <summary>
    /// Updates UI on all consoles of this faction.
    /// </summary>
    private void UpdateAllFactionUi(AmbassadorConsoleComponent source)
    {
        SyncFaction(source);
        RefreshFactionUi(source);
    }

    private void RefreshFactionUi(AmbassadorConsoleComponent source)
    {
        var query = EntityQueryEnumerator<AmbassadorConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.FactionName == source.FactionName)
                UpdateUiState(uid, comp);
        }
    }

    // ---- Update tick ----

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        ProcessDeliveries(); // RUMC: the transports of the mother faction

        // the economy panel reads other consoles (taxes, tariffs), so open UIs still get a slow refresh
        var refreshUi = _timing.CurTime >= _nextUiRefresh;
        if (refreshUi)
            _nextUiRefresh = _timing.CurTime + UiRefreshInterval;

        // Only tick one console per faction to avoid double-charging.
        _tickedFactions.Clear();

        var query = EntityQueryEnumerator<AmbassadorConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            // the faction's first console refreshes every console in the faction below
            if (!_tickedFactions.Add(comp.FactionName))
                continue;

            var oldBudget = comp.Budget;
            var oldEffects = (comp.EmbargoActive, comp.TradePactActive, comp.CommsJamActive,
                comp.SignalBoostActive, comp.SignalJamActive);

            comp.ReplenishTimer += frameTime;
            if (comp.ReplenishTimer >= comp.ReplenishInterval)
            {
                comp.ReplenishTimer -= comp.ReplenishInterval;
                comp.Budget += comp.ReplenishAmount;
            }

            // RUMC: what the faction has built in the cities of the map brings money (or costs it)
            if (_worldMarket.ClaimIncome(comp.FactionName) is var cityIncome && cityIncome != 0f)
                comp.Budget = Math.Max(0f, comp.Budget + cityIncome);

            // RUMC: crates that the missions of the mother faction have given are put down at the console
            foreach (var crate in _worldMarket.ClaimCrates(comp.FactionName))
                Spawn(crate, Transform(uid).Coordinates);
            if (comp.EmbargoActive)
            {
                comp.EmbargoTimer += frameTime;
                if (comp.EmbargoTimer >= 60f)
                {
                    comp.EmbargoTimer -= 60f;
                    comp.Budget -= comp.EmbargoCostPerMinute;
                    if (comp.Budget < 0)
                    {
                        comp.Budget = 0;
                        comp.EmbargoActive = false;
                        AnnounceStatus(
                            Loc.GetString("ambassador-console-embargo-ended-no-funds", ("faction", comp.FactionName)),
                            comp.FactionName);
                    }
                }
            }
            if (comp.TradePactActive)
            {
                comp.TradePactTimer += frameTime;
                if (comp.TradePactTimer >= 60f)
                {
                    comp.TradePactTimer -= 60f;
                    comp.Budget -= comp.TradePactCostPerMinute;
                    if (comp.Budget < 0)
                    {
                        comp.Budget = 0;
                        comp.TradePactActive = false;
                        AnnounceStatus(
                            Loc.GetString("ambassador-console-trade-pact-ended-no-funds", ("faction", comp.FactionName)),
                            comp.FactionName);
                    }
                }
            }
            if (comp.CommsJamActive)
            {
                comp.CommsJamTimer += frameTime;
                if (comp.CommsJamTimer >= 60f)
                {
                    comp.CommsJamTimer -= 60f;
                    comp.Budget -= comp.CommsJamCostPerMinute;
                    if (comp.Budget < 0)
                    {
                        comp.Budget = 0;
                        comp.CommsJamActive = false;
                        AnnounceStatus(
                            Loc.GetString("ambassador-console-comms-jam-ended-no-funds", ("faction", comp.FactionName)),
                            comp.FactionName);
                    }
                }
            }
            if (comp.SignalBoostActive)
            {
                comp.SignalBoostTimer += frameTime;
                if (comp.SignalBoostTimer >= 60f)
                {
                    comp.SignalBoostTimer -= 60f;
                    comp.Budget -= comp.SignalBoostCostPerMinute;
                    if (comp.Budget < 0)
                    {
                        comp.Budget = 0;
                        comp.SignalBoostActive = false;
                    }
                }
            }
            if (comp.SignalJamActive)
            {
                comp.SignalJamTimer += frameTime;
                if (comp.SignalJamTimer >= 60f)
                {
                    comp.SignalJamTimer -= 60f;
                    comp.Budget -= comp.SignalJamCostPerMinute;
                    if (comp.Budget < 0)
                    {
                        comp.Budget = 0;
                        comp.SignalJamActive = false;
                    }
                }
            }

            UpdateSignalModifier();
            SyncFaction(comp);

            // used to rebuild and resend the BUI state every tick, which dirtied the UI
            // component and shipped it to everyone near the console 30 times a second
            var changed = comp.Budget != oldBudget ||
                          oldEffects != (comp.EmbargoActive, comp.TradePactActive, comp.CommsJamActive,
                              comp.SignalBoostActive, comp.SignalJamActive);
            if (changed || refreshUi)
                RefreshFactionUi(comp);
        }
    }

    private void AnnounceStatus(string message, string? factionName = null)
    {
        var sender = factionName != null
            ? Loc.GetString("ambassador-console-embassy-sender", ("faction", factionName))
            : Loc.GetString("ambassador-console-default-sender");
        _chat.DispatchGlobalAnnouncement(message, sender, playSound: true, announcementSound: MarineAnnouncementSound);
    }

    public void UpdateSignalModifier()
    {
        float multiplier = 1f;
        var ambQ = EntityQueryEnumerator<AmbassadorConsoleComponent>();
        while (ambQ.MoveNext(out _, out var a))
        {
            if (a.SignalBoostActive) multiplier = Math.Min(multiplier, a.SignalBoostMultiplier);
            if (a.SignalJamActive) multiplier = Math.Max(multiplier, a.SignalJamMultiplier);
        }
        _thirdParty.SetSignalIntervalMultiplier(multiplier);
    }

    public bool IsCommsJammed()
    {
        var ambQ = EntityQueryEnumerator<AmbassadorConsoleComponent>();
        while (ambQ.MoveNext(out _, out var a)) { if (a.CommsJamActive) return true; }
        return false;
    }

    public float GetSubmissionMultiplier()
    {
        float mult = 1f;
        var query = EntityQueryEnumerator<AmbassadorConsoleComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            if (comp.EmbargoActive)
                mult = Math.Min(mult, comp.EmbargoMultiplier);
            if (comp.TradePactActive)
                mult = Math.Max(mult, comp.TradePactMultiplier);
        }
        return mult;
    }

    private List<string> GetShuttleRadarList()
    {
        var queued = _thirdParty.GetQueuedThirdParties();
        var filtered = queued
            .Where(p => string.Equals(p.EntryMethod, "shuttle", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var names = filtered.Select(p => p.DisplayName ?? p.ID).ToList();
        for (int i = names.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (names[i], names[j]) = (names[j], names[i]);
        }
        return names;
    }

    private void OnCashInserted(EntityUid uid, AmbassadorConsoleComponent comp, EntInsertedIntoContainerMessage args)
    {
        var stackCount = 1;
        if (TryComp<StackComponent>(args.Entity, out var stack))
            stackCount = stack.Count;

        comp.Budget += stackCount;
        QueueDel(args.Entity);
        UpdateAllFactionUi(comp);
    }

    private void OnInteractUsing(EntityUid uid, AmbassadorConsoleComponent comp, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!_tag.HasTag(args.Used, CurrencyTag))
            return;

        args.Handled = true;

        var stackCount = 1;
        if (TryComp<StackComponent>(args.Used, out var stack))
            stackCount = stack.Count;

        comp.Budget += stackCount;
        QueueDel(args.Used);
        UpdateAllFactionUi(comp);
    }

    private void UpdateUiState(EntityUid uid, AmbassadorConsoleComponent comp)
    {
        // RUMC: a faction whose console is open is on the line
        if (_ui.IsUiOpen(uid, AmbassadorConsoleUi.Key) || _ui.IsUiOpen(uid, AmbassadorWorldMapUi.Key) || _ui.IsUiOpen(uid, AmbassadorMotherUi.Key) || _ui.IsUiOpen(uid, AmbassadorThirdPartyUi.Key))
            _worldMarket.MarkOnline(comp.FactionName);

        // nobody's looking, OnUiOpened rebuilds it on open anyway
        if (_ui.IsUiOpen(uid, AmbassadorConsoleUi.Key))
            SetConsoleState(uid, comp);

        if (_ui.IsUiOpen(uid, AmbassadorThirdPartyUi.Key))
            SetThirdPartyState(uid, comp);

        // RUMC: the world map
        if (_ui.IsUiOpen(uid, AmbassadorWorldMapUi.Key))
            SetWorldMapState(uid, comp);

        // RUMC: the exchange with the mother faction
        if (_ui.IsUiOpen(uid, AmbassadorMotherUi.Key))
            SetMotherState(uid, comp);
    }

    // RUMC: the window of the exchange with the mother faction: the money and the points of influence, and what can be ordered.
    private void SetMotherState(EntityUid uid, AmbassadorConsoleComponent comp)
    {
        _ui.SetUiState(uid, AmbassadorMotherUi.Key,
            new AmbassadorMotherState(comp.Budget, _worldMarket.PointsOf(comp.FactionName), WorldMarketSystem.MoneyPerPoint, _worldMarket.TrustOf(comp.FactionName),
                _worldMarket.StipendSeconds(), _worldMarket.StipendAmount(comp.FactionName), _worldMarket.BuildTasks(comp.FactionName), _worldMarket.BuildOffers(comp.FactionName, DropPoints(comp.FactionName))));
    }

    // RUMC: the places that a transport of the mother faction can be put down at: the consoles of the faction.
    private List<TargetEntry> DropPoints(string faction)
    {
        var points = new List<TargetEntry>();
        var query = EntityQueryEnumerator<AmbassadorConsoleComponent>();
        while (query.MoveNext(out var id, out var console))
        {
            if (console.FactionName != faction)
                continue;

            var position = Transform(id).LocalPosition;
            points.Add(new TargetEntry(id.Id.ToString(), $"{Loc.GetString("rumc-mother-drop-point")} {points.Count + 1} ({(int) position.X}, {(int) position.Y})"));
        }

        return points;
    }

    // RUMC: the transports that are on their way: what arrives is put down at the console that it was sent to
    private readonly List<(TimeSpan At, EntityUid Point, string Offer, string Faction)> _deliveries = new();

    private void ProcessDeliveries()
    {
        if (_deliveries.Count == 0)
            return;

        var now = _timing.CurTime;
        for (var i = _deliveries.Count - 1; i >= 0; i--)
        {
            var delivery = _deliveries[i];
            if (delivery.At > now)
                continue;

            _deliveries.RemoveAt(i);
            if (!Exists(delivery.Point) || !_proto.TryIndex<WorldMotherOfferPrototype>(delivery.Offer, out var offer))
                continue;

            var at = Transform(delivery.Point).Coordinates;
            if (offer.Crate is { } crate)
            {
                for (var n = 0; n < offer.Amount; n++)
                    Spawn(crate, at);
            }

            if (offer.Party is { } partyId && _proto.TryIndex(partyId, out var party) && _proto.TryIndex(party.PartySpawn, out var spawn))
                _thirdParty.SpawnThirdParty(party, spawn, false);

            AnnounceStatus(Loc.GetString("rumc-mother-delivery", ("name", Loc.GetString(offer.Name))), delivery.Faction);
        }
    }

    // RUMC: the exchange with the mother faction
    private void OnOpenMother(EntityUid uid, AmbassadorConsoleComponent comp, AmbassadorOpenMotherBuiMsg msg)
    {
        _ui.TryToggleUi(uid, AmbassadorMotherUi.Key, msg.Actor);
        SetMotherState(uid, comp);
    }

    // RUMC: an order of the mother faction, paid in points of influence: a company or workers are called in as a third party, food and medicine are put down at the console, a holiday from a
    // tax is put on. Several of the same can be ordered.
    private void OnMotherOrder(EntityUid uid, AmbassadorConsoleComponent comp, MotherOrderMessage msg)
    {
        if (!_proto.TryIndex<WorldMotherOfferPrototype>(msg.Offer, out var offer) || _worldMarket.OfferBlocked(comp.FactionName, offer) != null)
            return;

        switch (offer.Group)
        {
            case MotherOfferGroup.Pmc:
            case MotherOfferGroup.Workers:
            case MotherOfferGroup.Support:
                // A party that is the same for all but belongs to whoever calls it has one for each faction.
                var calledParty = offer.Parties.TryGetValue(comp.FactionName, out var byFaction) ? byFaction : offer.Party;
                if (calledParty is not { } partyId || !_proto.TryIndex(partyId, out var party) || !_proto.TryIndex(party.PartySpawn, out var spawn))
                    return;

                if (!_worldMarket.TrySpendPoints(comp.FactionName, _worldMarket.OfferCost(comp.FactionName, offer)))
                    return;

                _thirdParty.SpawnThirdParty(party, spawn, false);
                break;

            case MotherOfferGroup.Food:
            case MotherOfferGroup.Medicine:
                if (offer.Crate is not { } crate || !_worldMarket.TrySpendPoints(comp.FactionName, _worldMarket.OfferCost(comp.FactionName, offer)))
                    return;

                var at = Transform(uid).Coordinates;
                for (var i = 0; i < offer.Amount; i++)
                    Spawn(crate, at);

                break;

            case MotherOfferGroup.Tax:
                if (!_worldMarket.TrySpendPoints(comp.FactionName, _worldMarket.OfferCost(comp.FactionName, offer)))
                    return;

                _worldMarket.ApplyTaxBreak(comp.FactionName, offer);
                break;

            case MotherOfferGroup.Transport:
                // An evacuation takes people out of a city in trouble; the rest is put down at one of the consoles of the faction after the time that it takes.
                if (offer.Evacuation)
                {
                    if (!_worldMarket.EvacuateCity(comp.FactionName, msg.Target, _worldMarket.OfferCost(comp.FactionName, offer)))
                        return;

                    break;
                }

                if (!int.TryParse(msg.Target, out var raw))
                    return;

                var point = new EntityUid(raw);
                if (!Exists(point) || !TryComp<AmbassadorConsoleComponent>(point, out var pointConsole) || pointConsole.FactionName != comp.FactionName)
                    return;

                if (!_worldMarket.TrySpendPoints(comp.FactionName, _worldMarket.OfferCost(comp.FactionName, offer)))
                    return;

                _deliveries.Add((_timing.CurTime + TimeSpan.FromSeconds(offer.DelaySeconds), point, offer.ID, comp.FactionName));
                break;
        }

        _worldMarket.NoteMotherOrder(comp.FactionName, offer);
        UpdateAllFactionUi(comp);
    }

    // RUMC: the report that the mother faction asked for is sent
    private void OnMotherReport(EntityUid uid, AmbassadorConsoleComponent comp, MotherReportMessage msg)
    {
        if (_worldMarket.SubmitReport(comp.FactionName))
            UpdateAllFactionUi(comp);
    }

    // RUMC: what the faction does with the other factions and the cities (pacts, secret strikes, bids, joint works, support of a good thing, hiring a company against a threat), paid from the
    // budget of the console
    private void OnMapCommand(EntityUid uid, AmbassadorConsoleComponent comp, MapCommandMessage msg)
    {
        if (_worldMarket.MapCommand(comp.FactionName, msg.Kind, msg.A, msg.B, msg.C, msg.N, comp.Budget) is not { } cost)
            return;

        comp.Budget -= cost;
        UpdateAllFactionUi(comp);
    }

    // RUMC: the faction marks a region of the map as its interest
    private void OnZoneClaim(EntityUid uid, AmbassadorConsoleComponent comp, ZoneClaimMessage msg)
    {
        if (_worldMarket.ClaimZone(comp.FactionName, msg.Latitude, msg.Longitude))
            UpdateAllFactionUi(comp);
    }

    // RUMC: points of influence changed for money (the subsidy of the mother faction) at the set rate
    private void OnMotherSubsidy(EntityUid uid, AmbassadorConsoleComponent comp, MotherSubsidyMessage msg)
    {
        if (_worldMarket.Subsidy(comp.FactionName, msg.Points) is not { } money)
            return;

        comp.Budget += money;
        UpdateAllFactionUi(comp);
    }

    // RUMC: the window of the world map: the budget of the console and the cities with the favour of the colony in each.
    private void SetWorldMapState(EntityUid uid, AmbassadorConsoleComponent comp)
    {
        var cities = _worldMarket.BuildCities(comp.FactionName, comp.Budget, out var seed, out var roads);
        _ui.SetUiState(uid, AmbassadorWorldMapUi.Key, new AmbassadorWorldMapState(comp.Budget, _worldMarket.PointsOf(comp.FactionName), comp.FactionName, seed, cities, roads,
            _worldMarket.BuildZones(comp.FactionName), _worldMarket.ZoneCost(comp.FactionName), _worldMarket.BuildMapExtras(comp.FactionName, comp.Budget)));
    }

    // RUMC: the world map
    private void OnOpenMap(EntityUid uid, AmbassadorConsoleComponent comp, AmbassadorOpenMapBuiMsg msg)
    {
        _ui.TryToggleUi(uid, AmbassadorWorldMapUi.Key, msg.Actor);
        SetWorldMapState(uid, comp);
    }

    // RUMC: something done for a city of the map (dig wells, mend roads...), paid from the budget of the console
    private void OnCityAction(EntityUid uid, AmbassadorConsoleComponent comp, CityActionMessage msg)
    {
        if (_worldMarket.DoCityAction(comp.FactionName, msg.City, msg.Action, comp.Budget) is not { } cost)
            return;

        comp.Budget -= cost;
        UpdateAllFactionUi(comp);
    }

    // RUMC: a gift to a city of the map, paid from the budget of the console
    private void OnGiftCity(EntityUid uid, AmbassadorConsoleComponent comp, GiftCityMessage msg)
    {
        if (_worldMarket.GiftCity(comp.FactionName, msg.City, comp.Budget) is not { } cost)
            return;

        comp.Budget -= cost;
        UpdateAllFactionUi(comp);
    }

    private void SetConsoleState(EntityUid uid, AmbassadorConsoleComponent comp)
    {
        var econ = _adminConsole.BuildEconomyStatus();
        // Use cached radar results (blank until scanned)
        var state = new AmbassadorConsoleBuiState(
            comp.Budget, comp.EmbargoActive, comp.TradePactActive, comp.CommsJamActive,
            comp.SignalBoostActive, comp.SignalJamActive,
            comp.LastRadarScanResults, econ,
            comp.FactionName,
            comp.EmbargoCostPerMinute,
            comp.TradePactCostPerMinute,
            comp.CommsJamCostPerMinute,
            comp.SignalBoostCostPerMinute,
            comp.SignalJamCostPerMinute,
            comp.BroadcastCost,
            comp.RadarScanCost);
        _ui.SetUiState(uid, AmbassadorConsoleUi.Key, state);
    }

    private void SetThirdPartyState(EntityUid uid, AmbassadorConsoleComponent comp)
    {
        var thirdParties = new Dictionary<string, (string DisplayName, float Cost)>();
        foreach (var (id, cost) in comp.CallableParties)
        {
            if (_proto.TryIndex<ThirdPartyPrototype>(id, out var proto))
            {
                var displayName = proto.DisplayName ?? proto.ID;
                thirdParties[id] = (displayName, cost);
            }
        }
        var thirdPartyState = new AmbassadorThirdPartyBuiState(comp.Budget, thirdParties, comp.CalledParties)
        {
            CallsLeft = Math.Max(0, comp.MaxCallIns - comp.CalledParties.Count),
            NextCallInAt = comp.NextCallInAt,
        };
        _ui.SetUiState(uid, AmbassadorThirdPartyUi.Key, thirdPartyState);
    }

    private void OnUiOpened(EntityUid uid, AmbassadorConsoleComponent comp, BoundUIOpenedEvent args)
    {
        UpdateUiState(uid, comp);
    }

    private void OnOpenThirdParty(EntityUid uid, AmbassadorConsoleComponent comp, AmbassadorOpenThirdPartyBuiMsg msg)
    {
        _ui.TryToggleUi(uid, AmbassadorThirdPartyUi.Key, msg.Actor);
    }

    private void OnScanRadar(EntityUid uid, AmbassadorConsoleComponent comp, AmbassadorScanRadarBuiMsg msg)
    {
        if (comp.Budget < comp.RadarScanCost) return;
        comp.Budget -= comp.RadarScanCost;
        // Snapshot the current shuttle radar and cache it
        comp.LastRadarScanResults = GetShuttleRadarList();
        UpdateAllFactionUi(comp);
    }

    private void OnWithdraw(EntityUid uid, AmbassadorConsoleComponent comp, AmbassadorWithdrawBuiMsg msg)
    {
        if (msg.Amount <= 0 || msg.Amount > comp.Budget) return;

        // RUMC: the first part of the account cannot be taken out as cash.
        if (comp.Budget - msg.Amount < comp.WithdrawFloor)
        {
            _popup.PopupEntity(Loc.GetString("rumc-ambassador-withdraw-locked", ("floor", (int) comp.WithdrawFloor)), uid, msg.Actor);
            return;
        }

        comp.Budget -= msg.Amount;
        _stack.SpawnMultipleNextToOrDrop(CashPrototype, (int) msg.Amount, uid);
        UpdateAllFactionUi(comp);
    }

    private void OnCallThirdParty(EntityUid uid, AmbassadorConsoleComponent comp, AmbassadorCallThirdPartyBuiMsg msg)
    {
        if (!comp.CallableParties.TryGetValue(msg.ThirdPartyId, out var cost)) return;
        if (comp.CalledParties.Contains(msg.ThirdPartyId)) return;
        if (comp.CalledParties.Count >= comp.MaxCallIns) return;
        if (_timing.CurTime < comp.NextCallInAt) return;
        if (comp.Budget < cost) return;
        if (!_proto.TryIndex<ThirdPartyPrototype>(msg.ThirdPartyId, out var partyProto)) return;
        if (!_proto.TryIndex(partyProto.PartySpawn, out var spawnProto)) return;
        _thirdParty.SpawnThirdParty(partyProto, spawnProto, false);

        comp.Budget -= cost;
        comp.CalledParties.Add(msg.ThirdPartyId);
        comp.NextCallInAt = _timing.CurTime + comp.CallInCooldown;
        UpdateAllFactionUi(comp);
    }

    private void OnToggleEmbargo(EntityUid uid, AmbassadorConsoleComponent comp, AmbassadorToggleEmbargoBuiMsg msg)
    {
        comp.EmbargoActive = !comp.EmbargoActive;
        if (comp.EmbargoActive)
        {
            comp.EmbargoTimer = 0f;
            comp.TradePactActive = false;
            AnnounceStatus(
                Loc.GetString("ambassador-console-embargo-enabled", ("faction", comp.FactionName)),
                comp.FactionName);
        }
        else
        {
            AnnounceStatus(
                Loc.GetString("ambassador-console-embargo-disabled", ("faction", comp.FactionName)),
                comp.FactionName);
        }
        UpdateAllFactionUi(comp);
    }

    private void OnToggleTradePact(EntityUid uid, AmbassadorConsoleComponent comp, AmbassadorToggleTradePactBuiMsg msg)
    {
        comp.TradePactActive = !comp.TradePactActive;
        if (comp.TradePactActive)
        {
            comp.TradePactTimer = 0f;
            comp.EmbargoActive = false;
            AnnounceStatus(
                Loc.GetString("ambassador-console-trade-pact-enabled", ("faction", comp.FactionName)),
                comp.FactionName);
        }
        else
        {
            AnnounceStatus(
                Loc.GetString("ambassador-console-trade-pact-disabled", ("faction", comp.FactionName)),
                comp.FactionName);
        }
        UpdateAllFactionUi(comp);
    }

    private void OnToggleSignalBoost(EntityUid uid, AmbassadorConsoleComponent comp, AmbassadorToggleSignalBoostBuiMsg msg)
    {
        comp.SignalBoostActive = !comp.SignalBoostActive;
        if (comp.SignalBoostActive)
        {
            comp.SignalBoostTimer = 0f;
            comp.SignalJamActive = false;
        }
        UpdateSignalModifier();
        UpdateAllFactionUi(comp);
    }

    private void OnToggleSignalJam(EntityUid uid, AmbassadorConsoleComponent comp, AmbassadorToggleSignalJamBuiMsg msg)
    {
        comp.SignalJamActive = !comp.SignalJamActive;
        if (comp.SignalJamActive)
        {
            comp.SignalJamTimer = 0f;
            comp.SignalBoostActive = false;
        }
        UpdateSignalModifier();
        UpdateAllFactionUi(comp);
    }

    private void OnBroadcast(EntityUid uid, AmbassadorConsoleComponent comp, AmbassadorBroadcastBuiMsg msg)
    {
        if (string.IsNullOrWhiteSpace(msg.Message)) return;
        if (comp.Budget < comp.BroadcastCost) return;
        comp.Budget -= comp.BroadcastCost;
        var sender = Loc.GetString("ambassador-console-embassy-sender", ("faction", comp.FactionName));
        _chat.DispatchGlobalAnnouncement(msg.Message, sender, playSound: true, announcementSound: MarineAnnouncementSound);
        _radio.SendRadioMessage(uid, msg.Message, "colonyAlert", uid);
        UpdateAllFactionUi(comp);
    }

    private void OnToggleCommsJam(EntityUid uid, AmbassadorConsoleComponent comp, AmbassadorToggleCommsJamBuiMsg msg)
    {
        comp.CommsJamActive = !comp.CommsJamActive;
        if (comp.CommsJamActive)
        {
            comp.CommsJamTimer = 0f;
            AnnounceStatus(
                Loc.GetString("ambassador-console-comms-jam-enabled", ("faction", comp.FactionName)),
                comp.FactionName);
        }
        else
        {
            AnnounceStatus(
                Loc.GetString("ambassador-console-comms-jam-disabled", ("faction", comp.FactionName)),
                comp.FactionName);
        }
        UpdateAllFactionUi(comp);
    }
}
