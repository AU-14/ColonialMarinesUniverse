using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Yautja;

// hunt prey night vision gear the console has burned out. the NV components are already gone, this is only here
// to tell whoever tries it why nothing happens
[RegisterComponent, NetworkedComponent]
public sealed partial class YautjaPreyJammedOpticsComponent : Component;
