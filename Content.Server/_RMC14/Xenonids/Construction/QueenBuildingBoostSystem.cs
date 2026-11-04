using Content.Server.GameTicking;
using Content.Shared._RMC14.CCVar; // CMU14
using Content.Shared._RMC14.QueenSpawned;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.GameTicking;
using Robust.Shared.Configuration; // CMU14

namespace Content.Server._RMC14.Xenonids.Construction;

public sealed partial class QueenBuildingBoostSystem : EntitySystem
{
    [Dependency] private  GameTicker _gameTicker = default!;
    [Dependency] private IConfigurationManager _config = default!; // CMU14

    // CMU14: superseded by the CVar-backed fields below
    //private static readonly TimeSpan QueenBoostDuration = TimeSpan.FromMinutes(30);
    //private const float QueenBoostSpeedMultiplier = 0.5f;
    //private const float QueenBoostRemoteRange = 50f;

    private bool _boostEnabled = true; // CMU14: rmc.queen_building_boost
    private TimeSpan _boostDuration = TimeSpan.FromMinutes(30); // CMU14: rmc.queen_building_boost_duration_minutes
    private float _boostSpeedMultiplier = 5f / 6f; // CMU14: rmc.queen_building_boost_speed_multiplier
    private float _boostRemoteRange = 50f; // CMU14: rmc.queen_building_boost_remote_range

    private bool _boostExpired;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<QueenSpawnedEvent>(OnQueenSpawned);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        Subs.CVar(_config, RMCCVars.RMCQueenBuildingBoost, v => _boostEnabled = v, true); // CMU14
        Subs.CVar(_config, RMCCVars.RMCQueenBuildingBoostDurationMinutes, v => _boostDuration = TimeSpan.FromMinutes(v), true); // CMU14
        Subs.CVar(_config, RMCCVars.RMCQueenBuildingBoostSpeedMultiplier, v => _boostSpeedMultiplier = v, true); // CMU14
        Subs.CVar(_config, RMCCVars.RMCQueenBuildingBoostRemoteRange, v => _boostRemoteRange = v, true); // CMU14
    }

    private void OnQueenSpawned(QueenSpawnedEvent args)
    {
        if (_boostExpired ||
            !_boostEnabled) // CMU14
            return;

        ApplyQueenBoost(args.Queen);
    }

    private void ApplyQueenBoost(EntityUid queen)
    {
        var construction = EntityManager.System<SharedXenoConstructionSystem>();

        construction.GiveQueenBoost(
            queen,
            _boostSpeedMultiplier, // CMU14
            _boostRemoteRange); // CMU14

        Logger.GetSawmill("content").Info($"Queen building boost applied to {queen}");
    }

    public override void Update(float frameTime)
    {
        if (_boostExpired)
            return;

        if (_gameTicker.RunLevel != GameRunLevel.InRound)
            return;

        if (_gameTicker.RoundDuration() < _boostDuration) // CMU14
            return;

        _boostExpired = true;
        RemoveQueenBoosts();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        _boostExpired = false;
    }

    private void RemoveQueenBoosts()
    {
        var construction = EntityManager.System<SharedXenoConstructionSystem>();

        var queens = EntityQueryEnumerator<QueenBuildingBoostComponent>();

        while (queens.MoveNext(out var queen, out _))
        {
            construction.RemoveQueenBoost(queen);

            Logger.GetSawmill("content").Info($"Removed queen building boost from {queen}");
        }
    }
}
