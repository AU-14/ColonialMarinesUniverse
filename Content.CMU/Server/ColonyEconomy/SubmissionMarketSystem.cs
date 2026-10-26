using Content.Server.CMU14.Ambassador;
using Content.Server.Radio.EntitySystems;
using Content.Shared.CCVar;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.GameTicking;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.ColonyEconomy;

// supply and demand for the submission points. selling a commodity piles up pressure that
// pushes its price down, the pressure bleeds off over time, and now and then an off-world
// buyer booms or gluts one commodity. one market for the whole round, every point shares it
public sealed partial class SubmissionMarketSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private AmbassadorConsoleSystem _ambassador = default!;
    [Dependency] private CorporateConsoleSystem _corporate = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private SubmissionCommoditySystem _commodity = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private const int HistoryLength = 30;
    private static readonly TimeSpan SampleInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan UiInterval = TimeSpan.FromSeconds(1);

    private sealed class MarketEntry
    {
        public float Pressure;
        public TimeSpan Updated;
        public CommodityEventKind Event;
        public TimeSpan EventEnds;
        public readonly List<float> History = new();
    }

    private readonly Dictionary<string, MarketEntry> _entries = new();
    private readonly List<CMUCommodityPrototype> _candidates = new();

    private bool _enabled;
    private float _saturation;
    private float _halfLife;
    private float _intervalMin;
    private float _intervalMax;
    private float _duration;
    private float _boom;
    private float _glut;
    private float _boomChance;

    private TimeSpan _nextSample;
    private TimeSpan _nextEvent;
    private TimeSpan _nextUi;
    private bool _uiDirty;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<SubmissionStorageComponent, BoundUIOpenedEvent>(OnUiOpened);

        Subs.CVar(_config, CCVars.CMUMarketEnabled, v => _enabled = v, true);
        Subs.CVar(_config, CCVars.CMUMarketSaturation, v => _saturation = v, true);
        Subs.CVar(_config, CCVars.CMUMarketRecoveryHalfLife, v => _halfLife = v, true);
        Subs.CVar(_config, CCVars.CMUMarketEventIntervalMin, v => _intervalMin = v, true);
        Subs.CVar(_config, CCVars.CMUMarketEventIntervalMax, v => _intervalMax = v, true);
        Subs.CVar(_config, CCVars.CMUMarketEventDuration, v => _duration = v, true);
        Subs.CVar(_config, CCVars.CMUMarketBoomMultiplier, v => _boom = v, true);
        Subs.CVar(_config, CCVars.CMUMarketGlutMultiplier, v => _glut = v, true);
        Subs.CVar(_config, CCVars.CMUMarketBoomChance, v => _boomChance = v, true);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _entries.Clear();
        _nextSample = TimeSpan.Zero;
        _nextEvent = TimeSpan.Zero;
        _uiDirty = false;
    }

    // what one base dollar of this commodity pays right now, before trade pacts and tariff
    public float GetMultiplier(CMUCommodityPrototype commodity)
    {
        if (!_enabled)
            return 1f;

        var entry = GetEntry(commodity);
        var saturation = commodity.Saturation ?? _saturation;
        var multiplier = saturation > 0f ? 1f / (1f + entry.Pressure / saturation) : 1f;
        return multiplier * EventMultiplier(entry.Event);
    }

    private float EventMultiplier(CommodityEventKind kind)
    {
        return kind switch
        {
            CommodityEventKind.Boom => _boom,
            CommodityEventKind.Glut => _glut,
            _ => 1f,
        };
    }

    public void RecordSale(CMUCommodityPrototype commodity, float baseValue)
    {
        if (!_enabled || baseValue <= 0f)
            return;

        GetEntry(commodity).Pressure += baseValue;
        _uiDirty = true;
    }

    // decay is applied lazily on read so idle commodities cost nothing per tick
    private MarketEntry GetEntry(CMUCommodityPrototype commodity)
    {
        var now = _timing.CurTime;
        if (!_entries.TryGetValue(commodity.ID, out var entry))
        {
            entry = new MarketEntry { Updated = now };
            _entries[commodity.ID] = entry;
        }

        var minutes = (float) (now - entry.Updated).TotalMinutes;
        if (minutes > 0f && entry.Pressure > 0f)
        {
            entry.Pressure = _halfLife > 0f
                ? entry.Pressure * MathF.Pow(0.5f, minutes / _halfLife)
                : 0f;
        }

        entry.Updated = now;

        if (entry.Event != CommodityEventKind.None && now >= entry.EventEnds)
        {
            entry.Event = CommodityEventKind.None;
            _uiDirty = true;
        }

        return entry;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!_enabled)
            return;

        var now = _timing.CurTime;
        if (now >= _nextSample)
        {
            _nextSample = now + SampleInterval;
            foreach (var commodity in _commodity.Commodities)
            {
                var multiplier = GetMultiplier(commodity);
                var history = GetEntry(commodity).History;
                history.Add(multiplier);
                if (history.Count > HistoryLength)
                    history.RemoveAt(0);
            }

            _uiDirty = true;
        }

        if (now >= _nextEvent)
        {
            // first pass just schedules, so nothing fires the moment the round starts
            if (_nextEvent != TimeSpan.Zero)
                TryStartEvent();

            var min = MathF.Max(1f, _intervalMin);
            var max = MathF.Max(min, _intervalMax);
            _nextEvent = now + TimeSpan.FromMinutes(_random.NextFloat(min, max));
        }

        if (_uiDirty && now >= _nextUi)
        {
            _nextUi = now + UiInterval;
            _uiDirty = false;
            UpdateOpenUis();
        }
    }

    private void TryStartEvent()
    {
        // only things a point on the map actually buys, a boom nobody can sell into is noise
        _candidates.Clear();
        EntityUid? announcer = null;
        var query = EntityQueryEnumerator<SubmissionStorageComponent>();
        while (query.MoveNext(out var uid, out var point))
        {
            announcer ??= uid;
            foreach (var commodity in _commodity.Commodities)
            {
                if (!commodity.Events || commodity.Fallback || _candidates.Contains(commodity))
                    continue;

                if (_commodity.Buys(point, commodity) && GetEntry(commodity).Event == CommodityEventKind.None)
                    _candidates.Add(commodity);
            }
        }

        if (announcer is not { } source || _candidates.Count == 0)
            return;

        var picked = _random.Pick(_candidates);
        var entry = GetEntry(picked);
        var boom = _random.Prob(Math.Clamp(_boomChance, 0f, 1f));
        entry.Event = boom ? CommodityEventKind.Boom : CommodityEventKind.Glut;
        entry.EventEnds = _timing.CurTime + TimeSpan.FromMinutes(MathF.Max(1f, _duration));
        _uiDirty = true;

        var percent = (int) MathF.Round(MathF.Abs(EventMultiplier(entry.Event) - 1f) * 100f);
        var message = Loc.GetString(boom ? "cmu-submission-market-boom-announce" : "cmu-submission-market-glut-announce",
            ("commodity", Loc.GetString(picked.Name)),
            ("percent", percent),
            ("minutes", (int) MathF.Round(_duration)));
        _radio.SendRadioMessage(source, message, "colonyAlert", source);
    }

    private void OnUiOpened(Entity<SubmissionStorageComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (args.UiKey is SubmissionMarketUiKey)
            UpdateUi(ent);
    }

    private void UpdateOpenUis()
    {
        var query = EntityQueryEnumerator<SubmissionStorageComponent, UserInterfaceComponent>();
        while (query.MoveNext(out var uid, out var point, out var ui))
        {
            if (_ui.IsUiOpen((uid, ui), SubmissionMarketUiKey.Key))
                UpdateUi((uid, point));
        }
    }

    private void UpdateUi(Entity<SubmissionStorageComponent> ent)
    {
        var rows = new List<SubmissionMarketRow>();
        var elsewhere = new List<SubmissionMarketRow>();
        foreach (var commodity in _commodity.Commodities)
        {
            var buys = _commodity.Buys(ent.Comp, commodity);
            var entry = GetEntry(commodity);
            if (!buys && entry.Event == CommodityEventKind.None)
                continue;

            var kind = _enabled ? entry.Event : CommodityEventKind.None;
            var row = new SubmissionMarketRow(commodity.ID,
                GetMultiplier(commodity),
                entry.History.ToArray(),
                kind,
                EventMultiplier(kind),
                entry.EventEnds);

            if (buys)
                rows.Add(row);
            else if (_enabled)
                elsewhere.Add(row);
        }

        var state = new SubmissionMarketBuiState(rows, elsewhere, _ambassador.GetSubmissionMultiplier(), _corporate.GetTariff());
        _ui.SetUiState(ent.Owner, SubmissionMarketUiKey.Key, state);
    }
}
