using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.ZLevels.Core.Components;

/// <summary>
/// A fixed deck grid participating in its map's Z physics. Ordinary shuttles
/// still provide an isolated supporting surface and must not opt into this.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUZLevelDeckComponent : Component;
