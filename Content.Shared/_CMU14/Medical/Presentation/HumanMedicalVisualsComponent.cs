using System;
using Content.Shared._CMU14.Medical.Human.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Presentation;

[Flags, Serializable, NetSerializable]
public enum HumanMedicalRegionVisualFlags : ushort
{
    None = 0,
    Bandaged = 1 << 0,
    Splinted = 1 << 1,
    Casted = 1 << 2,
    Prosthetic = 1 << 3,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class HumanMedicalVisualsComponent : Component
{
    [DataField, AutoNetworkedField]
    public HumanMedicalRegionVisualFlags[] RegionFlags = new HumanMedicalRegionVisualFlags[HumanMedicalComponent.RegionSlotCount];

    [DataField, AutoNetworkedField]
    public int Revision;
}
