using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.ZLevels.Core.Components;

/// <summary>
/// Temporary marker for entities currently being processed by the server-side Z transition controller.
/// </summary>
[RegisterComponent, NetworkedComponent, UnsavedComponent, Access(typeof(CMUSharedZLevelsSystem))]
public sealed partial class CMUZFallingComponent : Component;
