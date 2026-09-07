using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Melee.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;

namespace Content.Shared.Weapons.Melee;

/// <summary>
/// This handles <see cref="MeleeSoundComponent"/>
/// </summary>
public sealed partial class MeleeSoundSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;

    public const float DamagePitchVariation = 0.05f;

    /// <summary>
    /// Plays the SwingSound from a weapon component
    /// for immediate feedback, misses and such
    /// (Swinging a weapon goes "whoosh" whether it hits or not)
    /// </summary>
    public void PlaySwingSound(EntityUid userUid,
        EntityUid weaponUid,
        MeleeWeaponComponent weaponComponent,
        bool predicted = true)
    {
        if (predicted)
            _audio.PlayPredicted(weaponComponent.SwingSound, weaponUid, userUid);
        else
            _audio.PlayPvs(weaponComponent.SwingSound, weaponUid);
    }

    /// <summary>
    /// Takes a "damageType" string as an argument and uses it to
    /// search one of the various Dictionaries in the MeleeSoundComponent
    /// for a sound to play, and falls back if that fails
    /// </summary>
    /// <param name="damageType"> Serves as a lookup key for a hit sound </param>
    /// <param name="hitSoundOverride"> A sound can be supplied by the <see cref="MeleeHitEvent"/> itself to override everything else </param>
    public void PlayHitSound(EntityUid targetUid,
        EntityUid? userUid,
        string? damageType,
        SoundSpecifier? hitSoundOverride,
        MeleeWeaponComponent weaponComponent,
        bool predicted = true)
    {
        var hitSound      = weaponComponent.HitSound;
        var noDamageSound = weaponComponent.NoDamageSound;

        if (weaponComponent.HitNonLivingSound != null && !HasComp<MobStateComponent>(targetUid))
            hitSound = weaponComponent.HitNonLivingSound;

        var playedSound = false;

        if (Deleted(targetUid))
            return;

        // hitting can obv destroy an entity so we play at coords and not following them
        var coords = Transform(targetUid).Coordinates;
        // Play sound based off of highest damage type.
        if (TryComp<MeleeSoundComponent>(targetUid, out var damageSoundComp))
        {
            if (damageType == null && damageSoundComp.NoDamageSound != null)
            {
                PlaySound(damageSoundComp.NoDamageSound, coords, userUid, damageSoundComp.NoDamageSound.Params.WithVariation(DamagePitchVariation), predicted);
                playedSound = true;
            }
            else if (damageType != null && damageSoundComp.SoundTypes?.TryGetValue(damageType, out var damageSoundType) == true)
            {
                PlaySound(damageSoundType, coords, userUid, damageSoundType.Params.WithVariation(DamagePitchVariation), predicted);
                playedSound = true;
            }
            else if (damageType != null && damageSoundComp.SoundGroups?.TryGetValue(damageType, out var damageSoundGroup) == true)
            {
                PlaySound(damageSoundGroup, coords, userUid, damageSoundGroup.Params.WithVariation(DamagePitchVariation), predicted);
                playedSound = true;
            }
        }

        // Use weapon sounds if the thing being hit doesn't specify its own sounds.
        if (!playedSound)
        {
            if (hitSoundOverride != null && damageType != null)
            {
                PlaySound(hitSoundOverride, coords, userUid, hitSoundOverride.Params.WithVariation(DamagePitchVariation), predicted);
                playedSound = true;
            }
            else if (hitSound != null && damageType != null)
            {
                PlaySound(hitSound, coords, userUid, hitSound.Params.WithVariation(DamagePitchVariation), predicted);
                playedSound = true;
            }
            else
            {
                PlaySound(noDamageSound, coords, userUid, noDamageSound.Params.WithVariation(DamagePitchVariation), predicted);
                playedSound = true;
            }
        }

        // Fallback to generic sounds.
        if (!playedSound)
        {
            switch (damageType)
            {
                // Unfortunately heat returns caustic group so can't just use the damagegroup in that instance.
                case "Burn":
                case "Heat":
                case "Radiation":
                case "Cold":
                    PlaySound(new SoundPathSpecifier("/Audio/Items/welder.ogg"), coords, userUid, AudioParams.Default.WithVariation(DamagePitchVariation), predicted);
                    break;
                // No damage, fallback to tappies
                case null:
                    PlaySound(new SoundCollectionSpecifier("WeakHit"), coords, userUid, AudioParams.Default.WithVariation(DamagePitchVariation), predicted);
                    break;
                case "Brute":
                    PlaySound(new SoundCollectionSpecifier("MetalThud"), coords, userUid, AudioParams.Default.WithVariation(DamagePitchVariation), predicted);
                    break;
            }
        }
    }

    private void PlaySound(SoundSpecifier? sound,
        EntityCoordinates coordinates,
        EntityUid? userUid,
        AudioParams audioParams,
        bool predicted)
    {
        if (predicted)
            _audio.PlayPredicted(sound, coordinates, userUid, audioParams);
        else
            _audio.PlayPvs(sound, coordinates, audioParams);
    }

}
