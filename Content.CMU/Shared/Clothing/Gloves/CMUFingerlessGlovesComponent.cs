using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Clothing.Gloves;

/// <summary>
///     Goes on a pair of fingerless gloves: the gloves listed in <see cref="From"/> can have their fingertips cut off
///     with a knife or wirecutters to turn into these.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUFingerlessGlovesComponent : Component
{
    [DataField(required: true)]
    public List<EntProtoId> From = new();

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(3);
}

[Serializable, NetSerializable]
public sealed partial class CMUCutGlovesDoAfterEvent : SimpleDoAfterEvent;
