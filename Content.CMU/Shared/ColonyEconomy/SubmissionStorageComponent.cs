// Content.Shared/CMU14/ColonyEconomy/SubmissionStorageComponent.cs
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.ColonyEconomy;

[RegisterComponent, NetworkedComponent]
public sealed partial class SubmissionStorageComponent : Component
{
    [DataField, ViewVariables]
    public Dictionary<ProtoId<TagPrototype>, float>? Rewards;

    // generic tags like Fruit, only paid when nothing in Rewards matches.
    // otherwise a tomato would sell at the fruit price
    [DataField, ViewVariables]
    public Dictionary<ProtoId<TagPrototype>, float>? FallbackRewards;
}
