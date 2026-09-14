using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Power;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUAutoTransferSwitchComponent : Component
{
    // Standby: mains healthy. Cranking: mains lost, backup spinning up.
    // Online: backup circuit supplying.
    [ViewVariables, AutoNetworkedField]
    public CMUAtsState State;

    [DataField]
    public TimeSpan CrankDelay = TimeSpan.FromSeconds(8);

    [DataField]
    public TimeSpan RevertDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public string OutputNode = "output";

    [DataField, AutoPausedField]
    public TimeSpan CrankEnd;

    [DataField, AutoPausedField]
    public TimeSpan RevertEnd;

    [DataField]
    public SoundSpecifier? CrankSound;

    [DataField]
    public SoundSpecifier? OnlineSound;
}

[Serializable, NetSerializable]
public enum CMUAtsState : byte
{
    Standby,
    Cranking,
    Online,
}

[Serializable, NetSerializable]
public enum CMUAtsVisuals : byte
{
    State,
}
