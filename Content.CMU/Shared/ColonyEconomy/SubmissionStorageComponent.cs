// Content.Shared/AU14/ColonyEconomy/SubmissionStorageComponent.cs
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.AU14.ColonyEconomy;

[RegisterComponent, NetworkedComponent]
public sealed partial class SubmissionStorageComponent : Component
{
    [DataField, ViewVariables]
    public Dictionary<ProtoId<TagPrototype>, float>? Rewards;
}
