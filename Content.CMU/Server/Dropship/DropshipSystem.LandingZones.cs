using Content.Shared._RMC14.Dropship;

namespace Content.Server._RMC14.Dropship;

public sealed partial class DropshipSystem
{
    private void OnForceOnForceDestinationMapInit(Entity<DropshipDestinationComponent> destination, ref MapInitEvent args)
    {
        if (_gameTicker.CurrentPreset?.ID.Equals("ForceOnForce", StringComparison.OrdinalIgnoreCase) == true &&
            destination.Comp.ForceOnForceFaction is { } faction)
            SetFactionController(destination, faction);
    }
}
