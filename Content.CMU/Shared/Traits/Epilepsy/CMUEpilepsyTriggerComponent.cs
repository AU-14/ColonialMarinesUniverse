using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Traits.Epilepsy;

/// <summary>
/// A flash that can set off a seizure in someone with <see cref="EpilepsyComponent"/> when it flashes them.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUEpilepsyTriggerComponent : Component
{
    [DataField]
    public float Chance = EpilepsySystem.FlashSeizureChance;
}
