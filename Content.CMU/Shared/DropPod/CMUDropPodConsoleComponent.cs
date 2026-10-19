using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.DropPod;

/// <summary>
/// Console that picks a planet insertion point on the tactical map and fires drop pods in its group.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUDropPodConsoleComponent : Component
{
    [DataField, AutoNetworkedField]
    public string Group = "default";

    /// <summary>
    /// Selected insertion tile on the planet tactical map.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Vector2i? Target;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Delay between consecutive pods of one salvo.
    /// </summary>
    [DataField]
    public TimeSpan LaunchStagger = TimeSpan.FromSeconds(0.5);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextLaunch;

    [DataField]
    public SoundSpecifier? LaunchSound;
}
