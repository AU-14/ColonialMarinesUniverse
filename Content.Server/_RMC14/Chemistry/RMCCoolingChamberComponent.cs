namespace Content.Server._RMC14.Chemistry;

[RegisterComponent]
public sealed partial class RMCCoolingChamberComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float CoolPerSecond = 160f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float TargetTemperature = 0.15f;

    // Skip one update after closing so PVS can finish moving contents into storage.
    public bool SkipNextTick;
}
