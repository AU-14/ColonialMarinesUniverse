using System.Linq;
using Content.Shared._AU14.Abominations;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._AU14.Abominations;

/// <summary>
/// Global flesh-nest spawning. Replaces the per-nest TimedSpawner with a
/// single server-wide timer: every tick, pick one random nest and spawn one
/// non-mimic abomination at it. The base interval is 4x the old single-nest
/// rate (360s instead of 90s), and each additional nest in the world makes
/// the global cadence 5% faster (linear).
/// </summary>
public sealed class AbominationNestSpawnSystem : EntitySystem
{
    /// <summary>Base interval with one nest placed.</summary>
    public static readonly TimeSpan BaseInterval = TimeSpan.FromSeconds(360);

    /// <summary>Rate multiplier added per extra nest beyond the first.</summary>
    public const float RatePerExtraNest = 0.05f;

    public static readonly EntProtoId[] SpawnPool =
    {
        "AU14AbominationSpider",
        "AU14AbominationGrunt",
        "AU14AbominationSkitter",
    };

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private TimeSpan _nextSpawnAt;

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (_nextSpawnAt > now)
            return;

        var nests = new List<EntityUid>();
        var query = EntityQueryEnumerator<AbominationFleshNestComponent>();
        while (query.MoveNext(out var uid, out _))
            nests.Add(uid);

        if (nests.Count == 0)
        {
            // No nests in the world; idle out the base interval before
            // checking again. Avoids re-querying every frame.
            _nextSpawnAt = now + BaseInterval;
            return;
        }

        // Each extra nest beyond the first speeds the spawn cadence by 5%.
        var rateMultiplier = 1f + (nests.Count - 1) * RatePerExtraNest;
        var interval = TimeSpan.FromSeconds(BaseInterval.TotalSeconds / rateMultiplier);

        var chosen = _random.Pick(nests);
        var proto = _random.Pick(SpawnPool);
        var coords = _transform.GetMapCoordinates(chosen);
        Spawn(proto, coords);

        _nextSpawnAt = now + interval;
    }
}
