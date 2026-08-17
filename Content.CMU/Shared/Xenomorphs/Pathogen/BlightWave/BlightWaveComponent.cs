using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.BlightWave;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedBlightWaveSystem))]
public sealed partial class CMUXenoBlightWaveComponent : Component
{
    [DataField, AutoNetworkedField]
    public float PlasmaCost = 200f;

    [DataField, AutoNetworkedField]
    public float Range = 7f;

    [DataField, AutoNetworkedField]
    public TimeSpan SuperSlowDuration = TimeSpan.FromSeconds(10);

    [DataField, AutoNetworkedField]
    public TimeSpan DazeDuration = TimeSpan.FromSeconds(4);

    /// <summary>How long point lights stay off after the wave.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan LightOffDuration = TimeSpan.FromSeconds(20);

    [DataField, AutoNetworkedField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_RMC14/Xeno/alien_queen_screech.ogg",
        AudioParams.Default.WithVolume(-5));

    [DataField, AutoNetworkedField]
    public EntProtoId? Effect = "CMEffectScreech";
}
