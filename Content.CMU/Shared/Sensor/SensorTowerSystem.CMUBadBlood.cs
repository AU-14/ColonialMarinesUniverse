namespace Content.Shared._RMC14.Sensor;

// CMU14 BadBlood: Bad Blood sabotage breaks a sensor tower through the tower's own state change, so the RMC state
// event (tactical map, objective chain) fires as it does for a random breakdown. No RMC body edit.
// docs/GraphAudit-BadBlood-ObjectivesPlan.md FLOW-509
public sealed partial class SensorTowerSystem
{
    /// <summary>
    /// Breaks a tower that is Off or On down to <paramref name="state"/> (a repair state). False when the tower
    /// already needs repairs or <paramref name="state"/> is not a repair state.
    /// </summary>
    public bool CMUBreak(Entity<SensorTowerComponent> tower, SensorTowerState state = SensorTowerState.Wrench)
    {
        if (tower.Comp.State < SensorTowerState.Off || state >= SensorTowerState.Off)
            return false;

        ChangeState(tower, state);
        return true;
    }
}
