namespace Content.Server.CMU14.Threats.Mobs.Wendigo.Lab;

/// <summary>
/// Added to a piece of human meat on its first bite, so the Wendigo procedure can count the meal once it is finished.
/// </summary>
[RegisterComponent, Access(typeof(CMUWendigoTransformationSystem))]
public sealed partial class CMUWendigoMealComponent : Component
{
    /// <summary>
    /// Who is eating this piece. Not the feeder: the food's finished event only names the feeder.
    /// </summary>
    [ViewVariables]
    public EntityUid? Eater;

    /// <summary>
    /// Whether <see cref="Eater"/> was starving when they took the first bite, before this meal's nutrition lands.
    /// </summary>
    [ViewVariables]
    public bool StartedStarving;
}
