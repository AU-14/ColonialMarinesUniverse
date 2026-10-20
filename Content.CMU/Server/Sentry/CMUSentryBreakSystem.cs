using Content.Server.Destructible;
using Content.Shared._RMC14.Sentry;
using Content.Shared.CMU14.Sentry;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible.Thresholds.Triggers;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Containers;

namespace Content.Server.CMU14.Sentry;

/// <summary>
///     Damage that would destroy a sentry breaks it instead: it shuts down, goes black, loses a quarter of the ammo
///     left in its magazine and stops taking damage until someone repairs it.
/// </summary>
public sealed class CMUSentryBreakSystem : EntitySystem
{
    private static readonly SoundSpecifier BreakSound = new SoundCollectionSpecifier("MetalBreak");

    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SentrySystem _sentry = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUSentryBreakableComponent, DamageChangedEvent>(OnDamageChanged,
            before: [typeof(DestructibleSystem)]);
    }

    private void OnDamageChanged(Entity<CMUSentryBreakableComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased ||
            HasComp<CMUSentryBrokenComponent>(ent) ||
            BreakPoint(ent) is not { } breakPoint)
        {
            return;
        }

        var total = _damageable.GetTotalDamage(ent.Owner);
        if (total < breakPoint)
            return;

        // Mark it broken first: bringing the damage back under the threshold raises this event again.
        EnsureComp<CMUSentryBrokenComponent>(ent);

        // Just short of destroyed, so the destructible threshold never fires.
        var scale = (breakPoint - FixedPoint2.New(1)).Float() / total.Float();
        _damageable.SetDamage(ent.Owner, _damageable.GetAllDamage(ent.Owner) * scale);

        if (TryComp(ent, out SentryComponent? sentry))
        {
            _sentry.TrySetMode((ent, sentry), sentry.Mode == SentryMode.Item ? SentryMode.Item : SentryMode.Off);
            LoseAmmo(ent, sentry);
        }

        _audio.PlayPvs(BreakSound, ent);
        _popup.PopupEntity(Loc.GetString("cmu-sentry-broken", ("sentry", ent.Owner)), ent, PopupType.MediumCaution);
    }

    /// <summary>The lowest damage that would destroy it.</summary>
    private FixedPoint2? BreakPoint(EntityUid uid)
    {
        if (!TryComp(uid, out DestructibleComponent? destructible))
            return null;

        FixedPoint2? lowest = null;
        foreach (var threshold in destructible.Thresholds)
        {
            if (threshold.Trigger is DamageTrigger trigger && (lowest == null || trigger.Damage < lowest))
                lowest = trigger.Damage;
        }

        return lowest;
    }

    private void LoseAmmo(Entity<CMUSentryBreakableComponent> ent, SentryComponent sentry)
    {
        if (!_container.TryGetContainer(ent, sentry.ContainerSlotId, out var container) ||
            container.ContainedEntities.Count == 0)
        {
            return;
        }

        var magazine = container.ContainedEntities[0];
        if (!TryComp(magazine, out BallisticAmmoProviderComponent? ammo))
            return;

        var lost = (int) (ammo.Count * ent.Comp.AmmoLoss);
        if (lost <= 0)
            return;

        _gun.SetBallisticUnspawned((magazine, ammo), Math.Max(0, ammo.UnspawnedCount - lost));
    }
}
