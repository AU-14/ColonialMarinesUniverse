using Content.Shared._RMC14.Marines;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Marines;

[RegisterComponent, NetworkedComponent]
[Access(typeof(WarshipSystem))]
public sealed partial class WarshipComponent : Component;
