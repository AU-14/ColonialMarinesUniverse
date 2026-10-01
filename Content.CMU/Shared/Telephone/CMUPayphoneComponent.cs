using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Telephone;

/// <summary>
/// A public payphone. Named after the area it stands in so each one is identifiable in the phone list.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUPayphoneComponent : Component;
