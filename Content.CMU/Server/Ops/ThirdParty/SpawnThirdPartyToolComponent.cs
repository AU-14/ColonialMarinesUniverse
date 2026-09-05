using Content.Shared.CMU14.Threats;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Ops.ThirdParty;

[RegisterComponent]
public sealed partial class SpawnThirdPartyToolComponent : Component
{
    [DataField("dropship")]
    public bool Dropship = true;

    [DataField("party", required: true)]
    public ProtoId<ThirdPartyPrototype> Party;
}
