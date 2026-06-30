using Robust.Shared.GameStates;
using Robust.Shared.Audio;

namespace Content.Shared._RMC14.Requisitions.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedRequisitionsSystem))]
public sealed partial class RequisitionsComputerComponent : Component
{
    [DataField]
    public EntityUid? Account;

    [DataField("soundIncomingSurplus")]
    public SoundSpecifier IncomingSurplus = new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");

    [DataField]
    public EntityUid? Platform;

    [DataField(required: true), AutoNetworkedField, AlwaysPushInheritance]
    public List<RequisitionsCategory> Categories = new();

    public readonly Dictionary<(int Category, int Order), RequisitionsStockStatus> Stock = new();

    public TimeSpan NextStockUiUpdate;

    [DataField]
    public bool IsLastInteracted = false;

    [DataField]
    public string Faction = "none";


}

public sealed class RequisitionsStockStatus
{
    public int Current;

    public TimeSpan NextReplenish;
}
