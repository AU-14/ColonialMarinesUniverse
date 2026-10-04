using Content.Shared._CMU14.ChemicalIrritants;

namespace Content.Server._CMU14.Weapons;

/// <summary>
/// Server-side system for chemical irritants (tear gas, pepper spray, etc.)
/// This registers and runs the shared system on the server.
/// </summary>
public sealed class ChemicalIrritantSystem : SharedChemicalIrritantSystem
{
    public override void Initialize()
    {
        base.Initialize();
        // Any server-specific logic can go here
    }
}
