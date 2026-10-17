using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Threats.Mobs.Biomorph;

/// <summary>
///     Applied to the *polymorphed* humanoid entity while a mimic is currently
///     wearing this profile. The Polymorph system handles all the original-form
///     bookkeeping (PolymorphedEntityComponent restores entity on revert); this
///     component just records which profile is active and when the disguise
///     expires.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class BiomorphMimicTransformedComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan ExpiresAt;

    [DataField, AutoNetworkedField]
    public BiomorphAssimilationProfile Profile = new();
}
