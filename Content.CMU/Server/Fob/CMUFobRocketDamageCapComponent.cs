using Content.Shared.FixedPoint;

namespace Content.Server.CMU14.Fob;

/// <summary>Damage still allowed from each rocket that directly hit this T3, including its delayed blast.</summary>
[RegisterComponent]
public sealed partial class CMUFobRocketDamageCapComponent : Component
{
    // These finite-ammunition records outlive their projectile: queued explosions retain the source UID.
    // Keeping them on the victim also prevents healing, another rocket or unrelated attacks resetting a budget.
    [ViewVariables] public Dictionary<EntityUid, FixedPoint2> Remaining = new();
}
