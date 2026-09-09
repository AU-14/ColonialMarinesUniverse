namespace Content.Server.CMU14.Chemistry.HydroTrayEffects;

[RegisterComponent]
public sealed partial class CMUChemicalMutationWhitelistComponent : Component
{
    public readonly HashSet<string> AllowedMutations = [];
}
