using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._AU14.Weapons.GunSwitch;

/// <summary>
///     The illegal "Switch" auto-sear chip (crafted at the Sapper's Workbench). While the chip is
///     engaged (via its AttachableToggleable action, which reads as the gun's "Switch" fire mode)
///     the host weapon fires absurdly fast, loses most of its accuracy, and every shot has a small
///     chance to jam the gun (see <see cref="GunJammedComponent"/>).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GunSwitchComponent : Component
{
    /// <summary>Rate-of-fire multiplier while engaged.</summary>
    [DataField]
    public float FireRateMultiplier = 4f;

    /// <summary>Accuracy multiplier while engaged (0.25 = a quarter of normal accuracy).</summary>
    [DataField]
    public float AccuracyMultiplier = 0.25f;

    /// <summary>Scatter cone multiplier while engaged: both the minimum and maximum spread grow.</summary>
    [DataField]
    public float ScatterMultiplier = 4f;

    /// <summary>Chance per shot to jam the host weapon while engaged.</summary>
    [DataField]
    public float JamChance = 0.10f;

    /// <summary>Chance per shot the whole gun blows up in the wielder's hands.</summary>
    [DataField]
    public float ExplodeChance = 0.01f;

    /// <summary>Piercing damage dealt to the wielder when the gun blows up.</summary>
    [DataField]
    public float ExplodeDamage = 20f;
}

/// <summary>
///     Lives on the GUN while the Switch is dumping: once a Switch-engaged gun fires a single shot,
///     it keeps firing on its own (even with the trigger released) until it runs dry, jams, blows
///     up, or leaves the shooter's hands. Server-only bookkeeping; removed automatically.
/// </summary>
[RegisterComponent]
public sealed partial class GunSwitchDumpingComponent : Component
{
    /// <summary>Who is (involuntarily) holding the trigger down.</summary>
    [DataField]
    public EntityUid User;

    /// <summary>Where the dump keeps firing toward (the last aimed coordinates).</summary>
    [DataField]
    public EntityCoordinates Target;

    /// <summary>When the gun last actually fired; the dump self-terminates if shots stop landing.</summary>
    [DataField]
    public TimeSpan LastShot;
}

/// <summary>
///     A jammed firearm: it will not shoot until someone racks it clear. Added to the GUN (not the
///     attachment) by <c>GunSwitchSystem</c> so the block works whatever happens to the chip.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GunJammedComponent : Component
{
    /// <summary>How long one racking attempt takes, in seconds.</summary>
    [DataField]
    public float RackTime = 1f;

    /// <summary>Chance a racking attempt fails and must be repeated.</summary>
    [DataField]
    public float RackFailChance = 0.6f;
}

/// <summary>DoAfter for racking a jammed gun clear.</summary>
[Serializable, NetSerializable]
public sealed partial class GunJamRackDoAfterEvent : SimpleDoAfterEvent
{
}
