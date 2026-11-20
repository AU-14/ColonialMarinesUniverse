namespace Content.Shared.CMU14.DamageTracking;

/// <summary>
/// Who last hurt this entity and when, stamped from its <c>DamageChangedEvent</c> (before the destructible
/// thresholds run, so a killing blow is stamped too). The origin is resolved back to its actor where possible
/// (a projectile to its shooter, a thrown item to its thrower, a held weapon to its holder). Destruction and
/// sabotage bookkeeping read it to credit whoever broke something. Server bookkeeping; not networked.
/// </summary>
[RegisterComponent]
public sealed partial class CMULastDamagedByComponent : Component
{
    [DataField]
    public EntityUid? Origin;

    [DataField]
    public TimeSpan At;
}
