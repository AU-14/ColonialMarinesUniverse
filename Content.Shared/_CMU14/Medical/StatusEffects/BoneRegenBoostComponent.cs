using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.StatusEffects;

/// <summary>
///     Sits on a <c>StatusEffectCMUBoneRegenBoost</c> entity. The bone
///     healing tick multiplies its per-tick integrity gain by
///     <see cref="Multiplier"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BoneRegenBoostComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Multiplier = 1.5f;
}
