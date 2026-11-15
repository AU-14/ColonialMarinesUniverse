using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Xenonids.Strain;

public sealed partial class XenoStrainComponent
{
    /// <summary>The unstrained caste this specialization can return to.</summary>
    [DataField, AutoNetworkedField]
    public EntProtoId? BaseCaste;
}
