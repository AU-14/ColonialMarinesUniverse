using Content.Shared.StatusEffect;

namespace Content.Shared.Stunnable;

/// <summary>
/// Compatibility entry points for RMC stun callers that predate entity-based status effects.
/// </summary>
public abstract partial class SharedStunSystem
{
    /// <summary>
    /// Applies or extends a stun using the legacy refresh contract.
    /// </summary>
    /// <remarks>
    /// Entity-based status effects do not expose a force-bypass API. The parameter is retained for source
    /// compatibility, but stun eligibility is still controlled by the <c>StatusEffectStunned</c> prototype.
    /// </remarks>
    public bool TryStun(EntityUid uid, TimeSpan time, bool refresh, bool force = false)
    {
        _ = force;

        return refresh
            ? TryUpdateStunDuration(uid, time)
            : TryAddStunDuration(uid, time);
    }

    /// <summary>
    /// Applies knockdown and stun using the legacy refresh contract.
    /// </summary>
    /// <remarks>
    /// <paramref name="force"/> is forwarded to the current knockdown attempt. Stun eligibility remains controlled
    /// by the entity-based status-effect prototype.
    /// </remarks>
    public bool TryParalyze(EntityUid uid, TimeSpan time, bool refresh, bool force = false)
    {
        var canCrawl = HasComp<CrawlerComponent>(uid);

        if (!TryKnockdown(uid, time, refresh, force: force))
            return false;

        // TryKnockdown already applies paralysis to entities that cannot crawl.
        return !canCrawl || TryStun(uid, time, refresh, force);
    }

    /// <summary>
    /// Source-compatibility shim for the remaining RMC caller that cached the removed status-effects component.
    /// </summary>
    public bool TryParalyze(
        EntityUid uid,
        TimeSpan time,
        bool refresh,
        StatusEffectsComponent? status,
        bool force = false)
    {
        _ = status;
        return TryParalyze(uid, time, refresh, force);
    }
}
