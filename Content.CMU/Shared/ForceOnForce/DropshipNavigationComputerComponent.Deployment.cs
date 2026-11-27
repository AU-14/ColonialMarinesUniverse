using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Dropship;

public sealed partial class DropshipNavigationComputerComponent
{
    [DataField, AutoNetworkedField]
    public TimeSpan? OpeningDeparture;
}
