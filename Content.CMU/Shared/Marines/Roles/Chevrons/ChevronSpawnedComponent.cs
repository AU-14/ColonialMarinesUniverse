namespace Content.Shared._AU14.Marines.Roles.Chevrons;

/// <summary>
/// Added to a mob after a chevron has been spawned for it, to prevent duplicate spawning on reconnect.
/// </summary>
[RegisterComponent]
public sealed partial class ChevronSpawnedComponent : Component;