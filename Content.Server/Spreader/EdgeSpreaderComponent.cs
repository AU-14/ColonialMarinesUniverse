using Content.Shared.Spreader;
using Robust.Shared.Prototypes;

namespace Content.Server.Spreader;

/// <summary>
/// Entity capable of becoming cloning and replicating itself to adjacent edges. See <see cref="SpreaderSystem"/>
/// </summary>
[RegisterComponent, Access(typeof(SpreaderSystem))]
public sealed partial class EdgeSpreaderComponent : Component
{
    [DataField(required:true)]
    public ProtoId<EdgeSpreaderPrototype> Id;

    /// <summary>
    /// Optional per-entity override for the spread rate. When unset or zero, the
    /// rate from the referenced <see cref="EdgeSpreaderPrototype"/> is used.
    /// </summary>
    [DataField]
    public float UpdatesPerSecond;
}
