using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Ordnance;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RMCOrdnanceAssemblyComponent : Component
{
    [DataField, AutoNetworkedField]
    public RMCOrdnancePartType? LeftPartType;

    [DataField, AutoNetworkedField]
    public RMCOrdnancePartType? RightPartType;

    [DataField, AutoNetworkedField]
    public bool IsLocked;

    [DataField, AutoNetworkedField]
    public float TimerDelay = 5f;

    [DataField, AutoNetworkedField]
    public uint SignalFrequency = 1280;

    [DataField, AutoNetworkedField]
    public float ProximityRange = 1.5f;
}
