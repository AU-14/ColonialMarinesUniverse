using Content.Shared._RMC14.Prototypes;
using Content.Shared.Construction.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Client.Construction.UI;

internal sealed partial class ConstructionMenuPresenter
{
    private IEnumerable<ConstructionPrototype> EnumerateRMCConstructionPrototypes()
    {
        return _prototypeManager.EnumerateCM<ConstructionPrototype>();
    }

    private static bool IsRMCConstruction(ConstructionPrototype prototype)
    {
        return prototype.RMCPrototype != null;
    }

    private static string? GetRMCConstructionActionText(ConstructionPrototype prototype)
    {
        return IsRMCConstruction(prototype)
            ? Loc.GetString("rmc-construction-build-here")
            : null;
    }
}
