using Content.Shared._RMC14.Prototypes;
using Content.Shared.Chemistry.Reagent;

namespace Content.Client.Guidebook.Controls;

public sealed partial class GuideReagentGroupEmbed
{
    private IEnumerable<ReagentPrototype> RMCEnumerateReagents(bool includeUpstream = false)
    {
        return includeUpstream
            ? _prototype.EnumeratePrototypes<ReagentPrototype>()
            : _prototype.EnumerateCM<ReagentPrototype>();
    }
}
