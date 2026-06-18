using Content.Shared._CMU14.Medical.Human.Data;

namespace Content.Shared._CMU14.Medical.Human.Components;

[RegisterComponent]
public sealed partial class AnatomyRegionComponent : Component
{
    [DataField]
    public BodyRegion Region;
}
