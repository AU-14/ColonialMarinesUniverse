using Content.Shared._RMC14.Xenonids.Projectile.Spit.Charge;
using Content.Shared.CMU14.Xenos.Despoiler;

namespace Content.Shared._RMC14.Xenonids.Projectile.Spit;

public sealed partial class XenoSpitSystem
{
    /// <summary>
    /// Starts the full effect of a new acid tier without replacing or refreshing stronger acid.
    /// </summary>
    public void ApplyAcidTier(EntityUid target, int tier, CMULingeringAcidData settings)
    {
        if (TryComp<UserAcidedComponent>(target, out var existing) && existing.ExpiresAt > _timing.CurTime)
        {
            if (existing.Tier >= tier)
                return;
        }

        var acid = EnsureComp<UserAcidedComponent>(target);
        acid.Tier = tier;
        acid.Combo = tier > 1;
        acid.Damage = settings.Damage;
        acid.DamageEvery = TimeSpan.FromSeconds(1);
        acid.ArmorPiercing = settings.ArmorPiercing;
        acid.Duration = settings.Duration;
        acid.NextDamageAt = _timing.CurTime;
        acid.ExpiresAt = _timing.CurTime + settings.Duration;
        Dirty(target, acid);
        UpdateAppearance((target, acid));
    }
}
