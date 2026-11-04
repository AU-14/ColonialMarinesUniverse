namespace Content.Server.CMU14.Threats;

/// <summary>Simulation-thread checkpoints for a force deployment; never holds an entity query across updates.</summary>
public enum ForceDeploymentStep : byte
{
    Prepared,
    Spawned,
    Completed,
}
