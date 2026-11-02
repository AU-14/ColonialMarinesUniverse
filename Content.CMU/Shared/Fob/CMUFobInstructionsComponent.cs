using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Fob;

/// <summary>Granted only to registered, enabled FOB equipment. Exposes public rules, never mission data.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUFobInstructionsComponent : Component;
