using Content.Shared._CMU14.ZLevels.Core.EntitySystems;

namespace Content.Shared._CMU14.ZLevels.Core.Components;

/// <summary>
/// Temporary marker for entities currently being processed by the server-side Z transition controller.
/// </summary>
[RegisterComponent, UnsavedComponent, Access(typeof(CMUSharedZLevelsSystem))]
public sealed partial class CMUZFallingComponent : Component;
