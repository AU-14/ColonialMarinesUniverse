using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Intel;

public sealed partial class ViewIntelObjectivesComponent
{
    [DataField("team"), AutoNetworkedField]
    public string Team = Content.Shared._RMC14.Intel.Team.None;
}
