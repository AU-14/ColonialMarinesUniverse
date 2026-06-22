using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Attachable;

[RegisterComponent, NetworkedComponent]
[Access(typeof(CMUSlingSystem))]
public sealed partial class CMUSlingItemComponent : Component;