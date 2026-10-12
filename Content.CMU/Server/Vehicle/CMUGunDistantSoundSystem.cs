using Content.Shared.CMU14.Vehicle;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Vehicle;

/// <summary>
/// Plays a gun's distant shot to listeners too far off to hear its own, placed on the map so it
/// still reaches them where the gun is out of their view.
/// </summary>
public sealed class CMUGunDistantSoundSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUGunDistantSoundComponent, GunShotEvent>(OnShot);
    }

    private void OnShot(Entity<CMUGunDistantSoundComponent> ent, ref GunShotEvent args)
    {
        var position = _transform.GetMapCoordinates(ent);
        if (position.MapId == MapId.Nullspace || _transform.GetMap(ent.Owner) is not { } map)
            return;

        var filter = Filter.Empty()
            .AddInRange(position, ent.Comp.MaxRange)
            .RemoveInRange(position, ent.Comp.MinRange);

        if (filter.Count == 0)
            return;

        var audioParams = ent.Comp.Sound.Params
            .WithReferenceDistance(ent.Comp.MinRange)
            .WithMaxDistance(ent.Comp.MaxRange);

        _audio.PlayStatic(ent.Comp.Sound, filter, new EntityCoordinates(map, position.Position), true, audioParams);
    }
}
