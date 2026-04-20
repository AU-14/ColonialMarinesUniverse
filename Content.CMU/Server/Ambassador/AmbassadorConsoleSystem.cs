using System.Linq;
using Content.Server.AU14.ThirdParty;
using Content.Server.Chat.Systems;
using Content.Server.Radio;
using Content.Server.Radio.EntitySystems;
using Content.Server.Stack;
using Content.Shared.AU14.Ambassador;
using Content.Shared.AU14.ColonyEconomy;
using Content.Shared.AU14.Threats;
using Content.Shared._RMC14.Intel.Tech;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared.Stacks;
using Content.Shared.Interaction;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.AU14.Ambassador;

public sealed class AmbassadorConsoleSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly AuThirdPartySystem _thirdParty = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ColonyEconomy.AdminConsoleSystem _adminConsole = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private static readonly SoundSpecifier MarineAnnouncementSound =
        new SoundPathSpecifier("/Audio/_RMC14/Announcements/Marine/notice2.ogg");

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
    /// Gets all ambassador consoles that share the same faction name.
    /// </summary>
    private List<(EntityUid Uid, AmbassadorConsoleComponent Comp)> GetFactionConsoles(string factionName)
    {
        var result = new List<(EntityUid, AmbassadorConsoleComponent)>();
        var query = EntityQueryEnumerator<AmbassadorConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.FactionName == factionName)
                result.Add((uid, comp));
        }
        return result;
    }

    /// <summary>
    /// Syncs shared state from source to all other consoles with the same faction.
    /// Budget, active statuses, and timers are synced.
    /// </summary>
    private void SyncFaction(AmbassadorConsoleComponent source)
    {
        var consoles = GetFactionConsoles(source.FactionName);
        foreach (var (uid, comp) in consoles)
        {
            if (comp == source)
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
            comp.CalledParties = new HashSet<string>(source.CalledParties);
        }
    }

    /// <summary>
    /// Updates UI on all consoles of this faction.
    /// </summary>
    private void UpdateAllFactionUi(AmbassadorConsoleComponent source)
    {
        SyncFaction(source);
        var consoles = GetFactionConsoles(source.FactionName);
        foreach (var (uid, comp) in consoles)
        {
            UpdateUiState(uid, comp);
        }
    }

    // ---- Update tick ----

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Only tick one console per faction to avoid double-charging.
        var tickedFactions = new HashSet<string>();

        var query = EntityQueryEnumerator<AmbassadorConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!tickedFactions.Add(comp.FactionName))
            {
                // Already ticked this faction — just update UI from synced state
                UpdateUiState(uid, comp);
                continue;
            }

            comp.ReplenishTimer += frameTime;
            if (comp.ReplenishTimer >= comp.ReplenishInterval)
            {
                comp.ReplenishTimer -= comp.ReplenishInterval;
                comp.Budget += comp.ReplenishAmount;
            }
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
                        AnnounceStatus($"Trade embargo by {comp.FactionName} has ended due to insufficient funds.", comp.FactionName);
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
                        AnnounceStatus($"Trade pact by {comp.FactionName} has ended due to insufficient funds.", comp.FactionName);
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
                        AnnounceStatus($"Communications jamming by {comp.FactionName} has ended due to insufficient funds.", comp.FactionName);
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
            UpdateAllFactionUi(comp);
        }
    }

    private void AnnounceStatus(string message, string? factionName = null)
    {
        var sender = factionName != null ? $"{factionName} Embassy" : "Ambassador Console";
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
        EntityManager.QueueDeleteEntity(args.Entity);
        UpdateAllFactionUi(comp);
    }

    private void OnInteractUsing(EntityUid uid, AmbassadorConsoleComponent comp, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!_tag.HasTag(args.Used, "Currency"))
            return;

        args.Handled = true;

        var stackCount = 1;
        if (TryComp<StackComponent>(args.Used, out var stack))
            stackCount = stack.Count;

        comp.Budget += stackCount;
        EntityManager.QueueDeleteEntity(args.Used);
        UpdateAllFactionUi(comp);
    }

    private void UpdateUiState(EntityUid uid, AmbassadorConsoleComponent comp)
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

        var thirdParties = new Dictionary<string, (string DisplayName, float Cost)>();
        foreach (var (id, cost) in comp.CallableParties)
        {
            if (_proto.TryIndex<AuThirdPartyPrototype>(id, out var proto))
            {
                var displayName = proto.DisplayName ?? proto.ID;
                thirdParties[id] = (displayName, cost);
            }
        }
        var thirdPartyState = new AmbassadorThirdPartyBuiState(comp.Budget, thirdParties, comp.CalledParties);
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
        comp.Budget -= msg.Amount;
        _stack.SpawnMultiple("RMCSpaceCash", (int)msg.Amount, uid);
        UpdateAllFactionUi(comp);
    }

    private void OnCallThirdParty(EntityUid uid, AmbassadorConsoleComponent comp, AmbassadorCallThirdPartyBuiMsg msg)
    {
        if (!comp.CallableParties.TryGetValue(msg.ThirdPartyId, out var cost)) return;
        if (comp.CalledParties.Contains(msg.ThirdPartyId)) return;
        if (comp.Budget < cost) return;
        if (!_proto.TryIndex<AuThirdPartyPrototype>(msg.ThirdPartyId, out var partyProto)) return;
        if (!_proto.TryIndex(partyProto.PartySpawn, out var spawnProto)) return;
        comp.Budget -= cost;
        comp.CalledParties.Add(msg.ThirdPartyId);
        _thirdParty.SpawnThirdParty(partyProto, spawnProto, false);
        UpdateAllFactionUi(comp);
    }

    private void OnToggleEmbargo(EntityUid uid, AmbassadorConsoleComponent comp, AmbassadorToggleEmbargoBuiMsg msg)
    {
        comp.EmbargoActive = !comp.EmbargoActive;
        if (comp.EmbargoActive)
        {
            comp.EmbargoTimer = 0f;
            comp.TradePactActive = false;
            AnnounceStatus($"A trade embargo has been activated by {comp.FactionName}. Submission point payouts are reduced by 20%.", comp.FactionName);
        }
        else
        {
            AnnounceStatus($"The trade embargo by {comp.FactionName} has been lifted.", comp.FactionName);
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
            AnnounceStatus($"A trade pact has been activated by {comp.FactionName}. Submission point payouts are increased by 20%.", comp.FactionName);
        }
        else
        {
            AnnounceStatus($"The trade pact by {comp.FactionName} has ended.", comp.FactionName);
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
        var sender = $"{comp.FactionName} Embassy";
        _chat.DispatchGlobalAnnouncement(msg.Message, sender, playSound: true, announcementSound: MarineAnnouncementSound);
        _radio.SendRadioMessage(uid, msg.Message, "colonyalert", uid);
        UpdateAllFactionUi(comp);
    }

    private void OnToggleCommsJam(EntityUid uid, AmbassadorConsoleComponent comp, AmbassadorToggleCommsJamBuiMsg msg)
    {
        comp.CommsJamActive = !comp.CommsJamActive;
        if (comp.CommsJamActive)
        {
            comp.CommsJamTimer = 0f;
            AnnounceStatus($"Planeside communications have been jammed by {comp.FactionName}. All radio transmissions are blocked.", comp.FactionName);
        }
        else
        {
            AnnounceStatus($"Communications jamming by {comp.FactionName} has been disabled. Radio transmissions are restored.", comp.FactionName);
        }
        UpdateAllFactionUi(comp);
    }
}

