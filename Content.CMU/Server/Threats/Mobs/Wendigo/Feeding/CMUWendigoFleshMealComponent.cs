namespace Content.Server.CMU14.Threats.Mobs.Wendigo.Feeding;

/// <summary>
/// Added to a piece of human flesh when a Wendigo bites it, so the heal lands once the piece is finished.
/// </summary>
[RegisterComponent, Access(typeof(CMUWendigoFleshHealSystem))]
public sealed partial class CMUWendigoFleshMealComponent : Component
{
    /// <summary>
    /// The Wendigo that took the last bite, or null if the last bite was someone else's.
    /// The food's finished event only names the feeder, not the eater.
    /// </summary>
    [ViewVariables]
    public EntityUid? Eater;
}
