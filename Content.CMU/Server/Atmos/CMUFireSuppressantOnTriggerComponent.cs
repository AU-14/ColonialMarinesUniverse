using Robust.Shared.GameStates;

namespace Content.Server.CMU14.Atmos;

/// <summary>
/// On trigger, snuffs the fire around the grenade: deletes RMC tile fires, kills
/// their hotspots and extinguishes burning mobs in radius. Pair with
/// ReleaseGasOnTrigger carrying a cold inert mix so the gas does not relight the
/// moment the burst is over.
/// </summary>
[RegisterComponent]
public sealed partial class CMUFireSuppressantOnTriggerComponent : Component
{
    [DataField]
    public float Radius = 4.5f;
}
