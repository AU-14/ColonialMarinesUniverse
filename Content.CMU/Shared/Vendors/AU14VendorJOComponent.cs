using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using System.Collections.Generic;
using Content.Shared._RMC14.Vendors;

namespace Content.Shared._AU14.Vendors;
/// <summary>
/// Component used by JOs Vendors to restrict duplicate kits. Stores successful vends based on Entry int.
/// Since only spec kits need this for porting, all logic specifically uses this component.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedCMAutomatedVendorSystem))]
public sealed partial class AU14VendorJOComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<EntProtoId, int> GlobalSharedVends = new();
}
