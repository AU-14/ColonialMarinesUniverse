using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;

/// <summary>
/// A cracked carapace (W7 2A Chitin Crack, also Desecrate on boneless xenofauna): <see cref="Armor"/> is
/// taken off the body's xeno armor until <see cref="ExpiresAt"/>. Applied through
/// <see cref="EWJoeArmorShredSystem.Shred"/>; RMC has no general armor-shred debuff to reuse.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(EWJoeArmorShredSystem))]
public sealed partial class EWJoeArmorShredComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Armor;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ExpiresAt;
}
