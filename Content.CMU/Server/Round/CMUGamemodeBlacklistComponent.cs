namespace Content.Server.CMU14.Round;

/// <summary>
/// Deletes the entity at MapInit based on the listed gamemode presets: a blacklist by default,
/// a whitelist when <see cref="Inverted"/> is set. Entities on maps that persist across rounds
/// are evaluated once, when they load.
/// Server only: the entity never reaches the client in modes where it is deleted.
/// </summary>
[RegisterComponent]
public sealed partial class CMUGamemodeBlacklistComponent : Component
{
    [DataField]
    public List<string> BlacklistedGamemodes = new();

    /// <summary>
    /// Treat <see cref="BlacklistedGamemodes"/> as a whitelist: delete in every mode except the listed ones.
    /// </summary>
    [DataField]
    public bool Inverted = false;
}
