using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Construction;

/// <summary>
///     Marks every stage of the field antenna mast assembly (and the finished mast itself). The construction
///     graph in comms.yml carries the steps; this component is what keeps untrained hands out of them, so a
///     half-raised mast cannot be finished - or a working one taken down - by anyone who wanders past with a
///     wrench. Sits on the stage entities rather than on the graph because construction graph steps have no
///     access to the user.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AU14MastAssemblyComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId<SkillDefinitionComponent> Skill = "RMCSkillEngineer";

    [DataField, AutoNetworkedField]
    public int RequiredSkillLevel = 2;

    /// <summary>
    ///     Whether examining tells an untrained onlooker they could not work on this. Wanted on the stages of
    ///     a job in progress, noise on a mast that is already standing and finished.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ExamineHint = true;
}
