using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Yautja.BadBlood;

// what hunger and thirst cost a Bad Blood beyond the speed and damage that the satiation components handle
[RegisterComponent, NetworkedComponent]
[Access(typeof(BadBloodSurvivalSystem))]
public sealed partial class BadBloodSurvivalComponent : Component
{
    [DataField]
    public float WellFedRegenMultiplier = 1.25f;

    [DataField]
    public float PeckishRegenMultiplier = 0.75f;

    [DataField]
    public float StarvingRegenMultiplier = 0.5f;

    [DataField]
    public float StarvingLimbRegrowMultiplier = 1.5f;
}
