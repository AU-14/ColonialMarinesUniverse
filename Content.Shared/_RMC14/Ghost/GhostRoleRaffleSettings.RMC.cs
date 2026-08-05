namespace Content.Shared.Ghost.Roles.Raffles;

public sealed partial class GhostRoleRaffleSettings
{
    /// <summary>
    /// How many seconds the round needs to be going on before the raffle can be finished.
    /// </summary>
    [DataField]
    public float RoundTimeRequirement;
}
