using Content.Shared._RMC14.Weather;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Weather;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.ZLevels.Weather;

/// <summary>
/// Keeps loaded game-map floors under the ground map's weather. Only the ground's weather cycle runs, and whatever
/// weather the ground has (cycle events or admin weather) is mirrored onto every floor.
/// </summary>
public sealed partial class CMUZLevelWeatherSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ISerializationManager _serialization = default!;
    [Dependency] private SharedWeatherSystem _weather = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    private static readonly TimeSpan SyncInterval = TimeSpan.FromSeconds(1);

    // A floor ending within this of the ground counts as matching, so it isn't re-timed on every sync.
    private static readonly TimeSpan EndTolerance = TimeSpan.FromSeconds(1);

    private TimeSpan _nextSync;

    public void FollowMap(EntityUid map, EntityUid source)
    {
        if (map == source)
            return;

        // Map saves often keep a cycle on every floor, and each one rolls its own events for the whole network.
        // The ground keeps one; a floor's cycle only moves down when the ground has none.
        if (TryComp<RMCWeatherCycleComponent>(map, out var cycle))
        {
            if (!HasComp<RMCWeatherCycleComponent>(source))
                AddComp(source, _serialization.CreateCopy(cycle, notNullableOverride: true));

            RemComp<RMCWeatherCycleComponent>(map);
        }

        EnsureComp<CMUZLevelWeatherComponent>(map).Source = source;
        Sync(map, source);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextSync)
            return;

        _nextSync = now + SyncInterval;

        // Only loaded extra floors are queried; reading the ground's weather also follows admin weather commands.
        var query = EntityQueryEnumerator<CMUZLevelWeatherComponent>();
        while (query.MoveNext(out var uid, out var follower))
        {
            if (!TerminatingOrDeleted(follower.Source))
                Sync(uid, follower.Source);
        }
    }

    private void Sync(EntityUid map, EntityUid source)
    {
        var now = _timing.CurTime;
        var sourceWeather = GetWeather(source);
        var mapWeather = GetWeather(map);

        foreach (var (proto, sourceEnd) in sourceWeather)
        {
            if (mapWeather.TryGetValue(proto, out var mapEnd) && EndsTogether(mapEnd, sourceEnd))
                continue;

            if (sourceEnd is { } end && end <= now)
                continue;

            _weather.TryAddWeather(map, proto, out _, sourceEnd - now);
        }

        foreach (var (proto, mapEnd) in mapWeather)
        {
            if (sourceWeather.ContainsKey(proto))
                continue;

            // Removing re-times the effect to its shutdown, so leave weather that is already fading out alone.
            if (mapEnd is { } end && end <= now + SharedWeatherSystem.ShutdownTime + EndTolerance)
                continue;

            _weather.TryRemoveWeather(map, proto);
        }
    }

    private Dictionary<EntProtoId, TimeSpan?> GetWeather(EntityUid map)
    {
        var weather = new Dictionary<EntProtoId, TimeSpan?>();
        if (!_statusEffects.TryEffectsWithComp<WeatherStatusEffectComponent>(map, out var effects))
            return weather;

        foreach (var effect in effects)
        {
            if (MetaData(effect).EntityPrototype?.ID is { } id)
                weather[id] = effect.Comp2.EndEffectTime;
        }

        return weather;
    }

    private static bool EndsTogether(TimeSpan? a, TimeSpan? b)
    {
        if (a is null || b is null)
            return a is null && b is null;

        return (a.Value - b.Value).Duration() <= EndTolerance;
    }
}
