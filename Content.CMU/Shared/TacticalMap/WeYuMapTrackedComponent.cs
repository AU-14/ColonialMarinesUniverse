using Content.Shared._RMC14.TacticalMap;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.TacticalMap;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedTacticalMapSystem))]
public sealed partial class WeYuMapTrackedComponent : Component;
