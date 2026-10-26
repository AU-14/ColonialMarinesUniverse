using Content.Shared.CMU14.Xenomorphs.Swarm;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Xenonids.Construction;

public sealed partial class SharedXenoConstructionSystem
{
    /// <summary>AI chooses from its native caste build list before performing the regular action.</summary>
    public bool TryChooseSwarmStructure(EntityUid lesser, EntProtoId structure)
    {
        if (!HasComp<CMUSwarmLesserComponent>(lesser) ||
            !TryComp<XenoConstructionComponent>(lesser, out var construction) ||
            !construction.CanBuild.Contains(structure))
            return false;

        construction.BuildChoice = structure;
        Dirty(lesser, construction);
        return true;
    }
}
