using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Xenonids.Evolution;

[RegisterComponent]
[Access(typeof(XenoEvolutionSystem))]
public sealed partial class CMUXenoEvolutionRefundComponent : Component
{
    public EntProtoId PreviousCaste;
    public TimeSpan EvolvedAt;
    public FixedPoint2 Points;
}
