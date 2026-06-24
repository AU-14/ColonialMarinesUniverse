using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._AU14.Abominations;

/// <summary>
/// Applied to the *polymorphed* humanoid entity while a mimic is currently
/// wearing this profile. The Polymorph system handles all the original-form
/// bookkeeping (PolymorphedEntityComponent restores entity on revert); this
/// component just records which profile is active and when the disguise
/// expires.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class AbominationMimicTransformedComponent : Component
{
    [DataField, AutoNetworkedField]
    public AbominationAssimilationProfile Profile = new();

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ExpiresAt;
}
