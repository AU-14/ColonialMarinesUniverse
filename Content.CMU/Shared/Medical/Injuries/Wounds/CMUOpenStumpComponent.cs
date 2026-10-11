using Content.Shared.Body.Part;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Medical.Injuries.Wounds;

/// <summary>
/// On the body part a limb was torn from. Each open stump bleeds arterially until it is closed with
/// surgery or the limb is reattached. A tourniquet on the stump, or on this part, stops the bleeding
/// while it stays on.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUOpenStumpComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<CMUStump> Stumps = new();

    [DataField]
    public TimeSpan NextBleed;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class CMUStump
{
    [DataField]
    public BodyPartType Type;

    [DataField]
    public BodyPartSymmetry Symmetry;

    /// <summary>A tourniquet is clamped on this stump.</summary>
    [DataField]
    public bool Clamped;

    /// <summary>What to give back when the stump tourniquet is taken off.</summary>
    [DataField]
    public EntProtoId? ClampRefund;
}
