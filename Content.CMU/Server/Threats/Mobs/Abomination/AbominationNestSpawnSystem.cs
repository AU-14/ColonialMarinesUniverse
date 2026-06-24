using Content.Shared._AU14.Abominations;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Threats.Mobs.Abomination;

/// <summary>
///     Global flesh-nest spawning. Every tick picks one random nest and spawns
///     one non-mimic abomination at it. The base interval is 5 minutes with one
///     nest, and each additional nest reduces the interval by 3 seconds, floored
///     at 30 seconds.
/// </summary>
public sealed partial class AbominationNestSpawnSystem : EntitySystem
{
    /// <summary>Seconds subtracted from the interval per extra nest beyond the first.</summary>
    public const double SecondsPerExtraNest = 3.0;

    /// <summary>Base interval with one nest placed.</summary>
    public static readonly TimeSpan BaseInterval = TimeSpan.FromSeconds(300);

    /// <summary>Minimum spawn interval regardless of nest count.</summary>
    public static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(30);

    public static readonly EntProtoId[] SpawnPool =
    {
        "AU14AbominationSpider",
        "AU14AbominationGrunt",
        "AU14AbominationSkitter"
    };

    private TimeSpan _nextSpawnAt;
    [Dependency] private IRobustRandom _random = default!;

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Update(float frameTime)
    {
        TimeSpan now = _timing.CurTime;

        if (_nextSpawnAt > now)
            return;

        var nests = new List<EntityUid>();
        EntityQueryEnumerator<AbominationFleshNestComponent> query
            = EntityQueryEnumerator<AbominationFleshNestComponent>();
        while (query.MoveNext(out EntityUid uid, out _))
        {
            nests.Add(uid);
        }

        if (nests.Count == 0)
        {
            // No nests in the world; idle out the base interval before
            // checking again. Avoids re-querying every frame.
            _nextSpawnAt = now + AbominationNestSpawnSystem.BaseInterval;

            return;
        }

        // Each extra nest beyond the first shaves 3 seconds off the interval, floored at 30 s.
        int extraNests = nests.Count - 1;
        double intervalSeconds = Math.Max(AbominationNestSpawnSystem.MinInterval.TotalSeconds,
            AbominationNestSpawnSystem.BaseInterval.TotalSeconds
          - extraNests * AbominationNestSpawnSystem.SecondsPerExtraNest);
        TimeSpan interval = TimeSpan.FromSeconds(intervalSeconds);

        EntityUid      chosen = _random.Pick(nests);
        EntProtoId     proto  = _random.Pick(AbominationNestSpawnSystem.SpawnPool);
        MapCoordinates coords = _transform.GetMapCoordinates(chosen);
        Spawn(proto, coords);

        _nextSpawnAt = now + interval;
    }
}