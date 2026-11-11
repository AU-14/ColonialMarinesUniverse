using Robust.Shared.Audio;

namespace Content.Server.CMU14.Explosion;

/// <summary>
/// Melee weapon that detonates on the first non-wielder it hits, dazing the wielder and blowing off some of
/// their limbs. Ported from RMC-14 PR #10875, which gibbed the wielder instead.
/// </summary>
[RegisterComponent]
public sealed partial class CMULungeMineComponent : Component
{
    /// <summary>Whether the detonation severs the wielder's limbs.</summary>
    [DataField]
    public bool SeversWielderLimbs = true;

    /// <summary>Most limbs that are actually severed per detonation. The head is never a candidate.</summary>
    [DataField]
    public int MaxSeveredLimbs = 3;

    [DataField]
    public TimeSpan DazeDuration = TimeSpan.FromSeconds(6);

    [DataField]
    public SoundSpecifier? TriggerSound = new SoundPathSpecifier("/Audio/Items/wirecutter.ogg");

    /// <summary>Chance for each tank within <see cref="CookOffRange"/> of the detonation to start an ammunition cook-off.</summary>
    [DataField]
    public float CookOffChance = 0.1f;

    [DataField]
    public float CookOffRange = 1.5f;
}
