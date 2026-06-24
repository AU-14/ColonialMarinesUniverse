using Content.Shared.Actions;
using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._AU14.Abominations.Abilities;

/// <summary>
/// Lunge / pounce ability shared by abominations that need to close distance:
/// spider's Pounce and grunt's Slam. The action applies a physics impulse
/// toward the target tile and, while AbominationLeapingComponent is alive,
/// the entity knocks down + damages mobs it collides with.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class AbominationLeapComponent : Component
{
    /// <summary>Maximum distance of the leap in tiles.</summary>
    [DataField, AutoNetworkedField]
    public float Range = 6f;

    /// <summary>Impulse magnitude applied at the start of the leap.</summary>
    [DataField, AutoNetworkedField]
    public float Strength = 25f;

    /// <summary>How long the entity stays in flight before the leap auto-ends.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan FlightDuration = TimeSpan.FromSeconds(0.6);

    /// <summary>Knockdown duration applied to mobs the leaper hits.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan KnockdownTime = TimeSpan.FromSeconds(2);

    /// <summary>Damage applied to mobs hit by the leap.</summary>
    [DataField, AutoNetworkedField]
    public DamageSpecifier Damage = new();

    /// <summary>Sound played at the start of the leap (loud roar / lunge audio).</summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier? LeapSound;

    /// <summary>Cooldown before the action becomes usable again, server-managed via ActionUseDelay.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan? NextUseAt;
}

/// <summary>
/// Live on the abomination while it is mid-leap. Removed when the duration
/// elapses or when a valid hit is registered.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class AbominationLeapingComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan EndsAt;

    [DataField, AutoNetworkedField]
    public TimeSpan KnockdownTime;

    [DataField, AutoNetworkedField]
    public DamageSpecifier Damage = new();
}

public sealed partial class AbominationLeapActionEvent : WorldTargetActionEvent;
