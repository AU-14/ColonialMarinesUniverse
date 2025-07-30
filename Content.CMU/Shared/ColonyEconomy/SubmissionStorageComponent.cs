// Content.Shared/AU14/ColonyEconomy/SubmissionStorageComponent.cs
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Toolshed.TypeParsers;

namespace Content.Shared.AU14.ColonyEconomy;

[RegisterComponent, NetworkedComponent]
public sealed partial class SubmissionStorageComponent : Component
{
    [DataField("rewardAmount")]
    public float RewardAmount = 20f;
}
