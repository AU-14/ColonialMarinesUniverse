using Content.Shared.Damage.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared.Damage.Components;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedDamageOtherOnHitSystem))]
public sealed partial class DamageOtherBlacklistComponent : Component
{
    /// <summary>Entities matching this whitelist will receive no damage from this item when thrown.</summary>
    [DataField(required: true)]
    public EntityWhitelist Blacklist = new();
}
