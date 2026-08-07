using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Intel.Tech;

public sealed partial class TechControlConsoleComponent
{
    [DataField("team"), AutoNetworkedField]
    public string Team = Content.Shared._RMC14.Intel.Team.None;
}
