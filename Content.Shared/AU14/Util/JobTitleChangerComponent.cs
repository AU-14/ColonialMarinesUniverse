using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

namespace Content.Shared.Au14.Util;

/// <summary>
/// When this component is equipped, it overrides the entity's job title (and optionally job icon) for display purposes.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class JobTitleChangerComponent : Component
{
    /// <summary>
    /// The job title to display while this component is equipped.
    /// </summary>
    [DataField("jobTitle", required: true)]
    public string JobTitle = string.Empty;

    /// <summary>
    /// If true, this will override any other job title sources (such as ID cards).
    /// </summary>
    [DataField("override")]
    public bool Override = true;
}

