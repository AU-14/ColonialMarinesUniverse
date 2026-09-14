namespace Content.Shared.Roles;

public sealed partial class JobPrototype
{
    // Explicit opt-in prevents event monsters and administrative spawns from touching a bank.
    [DataField] public bool CmuEconomyEnabled;
}
