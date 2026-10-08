namespace Content.Shared.CMU14.Atmos;

/// <summary>
/// Turns on the mob's internals right after it spawns, using the best gas tank it's carrying (including tanks in
/// worn storage). Removed once internals are running, or after <see cref="Attempts"/> tries.
/// </summary>
[RegisterComponent]
public sealed partial class CMUSpawnWithInternalsComponent : Component
{
    /// <summary>How many more times to try before giving up, in case gear is still being equipped.</summary>
    [DataField]
    public int Attempts = 10;
}
