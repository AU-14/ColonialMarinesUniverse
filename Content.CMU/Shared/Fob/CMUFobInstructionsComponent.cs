using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Fob;

/// <summary>Exposes public setup instructions on FOB equipment, never private mission data.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUFobInstructionsComponent : Component;
