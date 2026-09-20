using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Hijack;

/// <summary>Fuel pumps are destroyed permanently at 2500 damage, independent of APC power.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUHijackPumpComponent : Component
{
    [DataField, AutoNetworkedField] public bool Broken;
    [DataField] public float Health = 2500;
}

[Serializable, NetSerializable]
public enum CMUHijackPumpVisuals : byte
{
    Broken,
    Health,
}
