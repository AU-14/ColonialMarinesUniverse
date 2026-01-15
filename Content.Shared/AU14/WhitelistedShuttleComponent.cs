using Content.Shared._RMC14.Dropship;
using Robust.Shared.GameStates;

namespace Content.Shared.AU14.Round;


[RegisterComponent,NetworkedComponent]
public sealed partial class WhitelistedShuttleComponent: Component
{

    [DataField("faction", required: true)]
    public string? Faction  { get; set; } = default;
    [DataField("ShuttleType", required: false)]

   public DropshipDestinationComponent.DestinationType ShuttleType = DropshipDestinationComponent.DestinationType.Dropship;

}
