using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Doors;

/// <summary>
///     A firelock that slams shut on its own when air starts escaping past it or the area beside it is spaced. Once it
///     has sealed, only someone with access or a crowbar gets it open, and it shuts again a few seconds later. It opens
///     back up by itself once the air on both sides is safe again.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUFirelockSealComponent : Component
{
    /// <summary>Below this pressure, in kPa, on either side, the firelock seals.</summary>
    [DataField]
    public float LowPressure = 50f;

    /// <summary>A pressure difference across the firelock bigger than this, in kPa, means air is escaping.</summary>
    [DataField]
    public float PressureDifference = 20f;

    /// <summary>Played when the firelock slams shut because of the air, the same alarm a fire alarm sounds.</summary>
    [DataField]
    public SoundSpecifier? AlarmSound = new SoundPathSpecifier("/Audio/Machines/alarm.ogg");

    /// <summary>Whether the firelock needs power to work, like an upstream firelock.</summary>
    [DataField]
    public bool RequiresPower;

    /// <summary>How long a sealed firelock stays open after being pried before it shuts again.</summary>
    [DataField]
    public TimeSpan PriedOpenDuration = TimeSpan.FromSeconds(6);

    /// <summary>Whether the firelock shut itself to hold back escaping air.</summary>
    [DataField, AutoNetworkedField]
    public bool Sealed;

    /// <summary>When a sealed firelock that was opened shuts again.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan? CloseAt;
}
