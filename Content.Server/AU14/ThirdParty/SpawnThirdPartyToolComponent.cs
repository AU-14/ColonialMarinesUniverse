using Content.Shared.AU14.Threats;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Server.AU14.ThirdParty;

[RegisterComponent]
public sealed partial class SpawnThirdPartyToolComponent : Component
{
    [DataField("party", required: true)]
    public ProtoId<AuThirdPartyPrototype> Party = default!;

    [DataField("dropship")]
    public bool Dropship = true;
}
