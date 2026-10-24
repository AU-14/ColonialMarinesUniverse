using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using System;

namespace Content.Shared.CMU14.Hygiene;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMULaundryHamperComponent : Component;

[Serializable, NetSerializable]
public enum CMULaundryHamperVisuals : byte { Dragged, Body, Fill }

[RegisterComponent, NetworkedComponent]
public sealed partial class CMULaundryTransferTargetComponent : Component;
