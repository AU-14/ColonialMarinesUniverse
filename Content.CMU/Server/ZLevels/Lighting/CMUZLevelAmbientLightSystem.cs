using Content.Shared._NC14.DayNightCycle;
using Content.Shared._RMC14.Light;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.ZLevels.Lighting;

/// <summary>
/// Keeps loaded game-map floors on one sky instead of allowing serialized
/// per-floor day/night cycles to diverge from the ground map.
/// </summary>
public sealed class CMUZLevelAmbientLightSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(DayNightCycleSystem));
        UpdatesAfter.Add(typeof(RMCAmbientLightSystem));
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
