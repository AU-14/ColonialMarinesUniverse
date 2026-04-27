namespace Content.Shared._RMC14.Ordnance;

[RegisterComponent]
public sealed partial class RMCChemicalAmmoTransferComponent : Component
{
    [DataField]
    public string Solution = "chemicals";
}
