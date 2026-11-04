using System.Linq;
using Content.Shared._NC14.DayNightCycle;
using Content.Shared._RMC14.Light;
using Content.Shared._RMC14.Rules;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.ZLevels.Lighting;

/// <summary>
/// Keeps loaded game-map floors on one sky instead of allowing serialized
/// per-floor day/night cycles to diverge from the ground map.
/// </summary>
public sealed partial class CMUZLevelAmbientLightSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(DayNightCycleSystem));
        UpdatesAfter.Add(typeof(RMCAmbientLightSystem));
    }

    /// <summary>
    ///     Sets the ground map's sky from its planet's <c>daycycleenabled</c> flag, so every floor that follows it cycles
    ///     (or doesn't) together. Map saves often keep the cycle on an upper or lower floor instead of the ground; its
    ///     timings are moved to the ground. Maps that aren't a planet keep whatever lighting they were saved with.
    /// </summary>
    public void ApplyPlanetDayCycle(EntityUid ground, string gameMapId, IEnumerable<EntityUid> floors)
    {
        if (GetPlanet(gameMapId) is not { } planet)
            return;

        if (!planet.DaycycleEnabled)
        {
            RemComp<DayNightCycleComponent>(ground);
            return;
        }

        EnsureComp<MapLightComponent>(ground);
        if (HasComp<DayNightCycleComponent>(ground))
            return;

        var saved = floors
            .Where(floor => floor != ground)
            .Select(floor => CompOrNull<DayNightCycleComponent>(floor))
            .FirstOrDefault(cycle => cycle != null);

        var cycle = new DayNightCycleComponent();
        if (saved != null)
        {
            cycle.CycleDurationMinutes = saved.CycleDurationMinutes;
            cycle.TimeEntries = saved.TimeEntries.ToList();
        }

        AddComp(ground, cycle);
    }

    private RMCPlanetMapPrototypeComponent? GetPlanet(string gameMapId)
    {
        foreach (var proto in _prototypes.EnumeratePrototypes<EntityPrototype>())
        {
            if (!proto.Abstract &&
                proto.TryComp(out RMCPlanetMapPrototypeComponent? planet, EntityManager.ComponentFactory) &&
                planet.MapId == gameMapId)
            {
                return planet;
            }
        }

        return null;
    }

    public void FollowMap(EntityUid map, EntityUid source)
    {
        if (map == source)
            return;

        // The ground map owns the cycle (or fixed light). Old map saves contain
        // independent cycles on additional floors, even on tidally locked planets.
        RemComp<DayNightCycleComponent>(map);
        RemComp<RMCAmbientLightComponent>(map);
        var follower = EnsureComp<CMUZLevelAmbientLightComponent>(map);
        follower.Source = source;
        var light = EnsureComp<MapLightComponent>(map);
        // Do not add light to the source: SpaceLightSystem uses its absence to
        // recognize space maps later in PostGameMapLoad.
        if (TryComp<MapLightComponent>(source, out var sourceLight))
            CopyLight((map, light), sourceLight);
    }

    public override void Update(float frameTime)
    {
        // Only loaded extra floors are queried, not tiles or world entities.
        // Reading the final color also follows admin lighting and future controllers.
        var query = EntityQueryEnumerator<CMUZLevelAmbientLightComponent, MapLightComponent>();
        while (query.MoveNext(out var uid, out var follower, out var light))
        {
            if (TryComp<MapLightComponent>(follower.Source, out var sourceLight))
                CopyLight((uid, light), sourceLight);
        }
    }

    private void CopyLight(Entity<MapLightComponent> target, MapLightComponent source)
    {
        if (target.Comp.AmbientLightColor == source.AmbientLightColor)
            return;

        target.Comp.AmbientLightColor = source.AmbientLightColor;
        Dirty(target);
    }
}
