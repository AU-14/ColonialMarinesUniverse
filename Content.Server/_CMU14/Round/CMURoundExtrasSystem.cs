// CMU14 file: round-start extras for presets that don't run CMDistressSignalRule (CMU DistressSignal)
using Content.Server._RMC14.MapInsert;
using Content.Server._RMC14.Marines;
using Content.Server._RMC14.Rules;
using Content.Server.AU14.Round;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Maps;
using Content.Shared._RMC14.Rules;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Round;

/// <summary>
///     Round-start features the classic <see cref="CMDistressSignalRuleComponent"/> rule provides on its own:
///     map insert processing for the loaded planet, planet/operation names for the tactical map and marine
///     announcements, and the ARES greeting/planet announcements. Stays off while the classic rule is
///     active so none of it happens twice.
/// </summary>
public sealed class CMURoundExtrasSystem : EntitySystem
{
    [Dependency] private AuRoundSystem _auRound = default!;
    [Dependency] private CMDistressSignalRuleSystem _distressSignal = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private MapInsertSystem _mapInsert = default!;
    [Dependency] private MarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private static readonly TimeSpan GreetingDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MapAnnounceDelay = TimeSpan.FromSeconds(20);

    // Mirrors the CMDistressSignalRuleComponent.AresGreetingAudio default
    private static readonly SoundSpecifier GreetingAudio =
        new SoundPathSpecifier("/Audio/_RMC14/Announcements/ARES/ares_online.ogg");

    private bool _active;
    private bool _greetingDone;
    private bool _mapAnnounced;

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private void OnRoundStarting(RoundStartingEvent ev)
    {
        _greetingDone = false;
        _mapAnnounced = false;
        _active = !HasActiveDistressRule();

        if (!_active)
            return;

        // Mirrors the classic rule's SpawnXenoMap insert pass: pick the nightmare scenario,
        // then process every insert marker on the loaded planet
        var scenario = string.Empty;
        if (_auRound.GetSelectedPlanet()?.NightmareScenarios is { } scenarios)
            scenario = _mapInsert.SelectMapScenario(scenarios);

        _distressSignal.ActiveNightmareScenario = scenario;

        var inserts = EntityQueryEnumerator<MapInsertComponent>();
        while (inserts.MoveNext(out var uid, out var insert))
            _mapInsert.ProcessMapInsert((uid, insert));

        string? planetName = null;
        if (_auRound.GetSelectedPlanetId() is { } planetId &&
            _prototypes.TryIndex<EntityPrototype>(planetId, out var planet))
            planetName = planet.Name;

        _distressSignal.SetCmuRoundInfo(planetName);
    }

    public override void Update(float frameTime)
    {
        if (!_active || _mapAnnounced)
            return;

        if (_gameTicker.RunLevel != GameRunLevel.InRound)
            return;

        var elapsed = _gameTicker.RoundDuration();

        if (!_greetingDone &&
            elapsed >= GreetingDelay)
        {
            _greetingDone = true;
            _marineAnnounce.AnnounceARESStaging(
                null,
                "APOLLO Nominal. Low-power standby concluded. Crew awakening from transit cryosleep. Good morning.",
                GreetingAudio,
                "rmc-announcement-ares-online");
        }

        if (!_mapAnnounced &&
            elapsed >= MapAnnounceDelay)
        {
            _mapAnnounced = true;

            if (_auRound.GetSelectedPlanet()?.Announcement is { } announcement)
            {
                GameMapPrototype? shipProto = null;
                if (_auRound.GetSelectedGovforShip() is { } shipId)
                    _prototypes.TryIndex<GameMapPrototype>(shipId, out shipProto);

                _marineAnnounce.AnnounceARESStaging(null, announcement, null, "rmc-announcement-ares-map",
                    ship: _distressSignal.GetWarshipName(shipProto));
            }
        }
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _active = false;
        _greetingDone = false;
        _mapAnnounced = false;
    }

    private bool HasActiveDistressRule()
    {
        var query = EntityQueryEnumerator<ActiveGameRuleComponent, CMDistressSignalRuleComponent>();
        return query.MoveNext(out _, out _);
    }
}
