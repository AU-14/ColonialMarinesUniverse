using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Shared.CMU14.Fighter;

/// <summary>Exterior sounds reach nearby listeners beyond the sprite's small PVS radius.</summary>
public sealed class FighterAudioSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public EntityUid? PlayGround(SoundSpecifier sound, EntityCoordinates site, float range = 55, float volume = -2)
    {
        var listeners = Filter.Empty().AddInRange(_transform.ToMapCoordinates(site), range);
        return _audio.PlayStatic(sound, listeners, site, true, Exterior(range, volume))?.Entity;
    }

    public static AudioParams Exterior(float range = 55, float volume = -2) => AudioParams.Default
        .WithVolume(volume).WithMaxDistance(range).WithReferenceDistance(6).WithRolloffFactor(.85f);

    public static SoundSpecifier Release(FighterWeaponKind kind, bool cockpit = false) => new SoundPathSpecifier(kind switch
    {
        FighterWeaponKind.Gau => cockpit ? "/Audio/CMU14/Fighter/gau-cockpit.ogg" : "/Audio/CMU14/Fighter/gau-ground.ogg",
        FighterWeaponKind.Rockets => "/Audio/CMU14/Fighter/rocket-release.ogg",
        _ => "/Audio/CMU14/Fighter/missile-release.ogg",
    });

    public static SoundSpecifier Impact(FighterWeaponKind kind) => new SoundPathSpecifier(kind switch
    {
        FighterWeaponKind.Gau => "/Audio/CMU14/Fighter/gau-impact.ogg",
        FighterWeaponKind.Rockets => "/Audio/CMU14/Fighter/rocket-impact.ogg",
        _ => "/Audio/CMU14/Fighter/missile-impact.ogg",
    });
}
