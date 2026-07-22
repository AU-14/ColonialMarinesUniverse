using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Damage.Components;
using Content.Shared.GameTicking;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    private const int BurstFallbackGraceTicks = 2;
    private const int MaxBurstFallbackPingMilliseconds = 500;

    [Dependency] private CMGunSystem _rmcGun = default!;

    private readonly Dictionary<EntityUid, BurstFallback> _burstFallbacks = new();

    private void InitializeAutoFire()
    {
        SubscribeLocalEvent<GunComponent, EntityTerminatingEvent>(OnGunTerminating);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private void OnGunTerminating(Entity<GunComponent> ent, ref EntityTerminatingEvent args)
    {
        _burstFallbacks.Remove(ent);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        _burstFallbacks.Clear();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        /*
         * On server because client doesn't want to predict other's guns.
         */

        // Automatic firing without stopping if the AutoShootGunComponent component is exist and enabled
        var query = EntityQueryEnumerator<GunComponent>();

        while (query.MoveNext(out var uid, out var gun))
        {
            if (!gun.BurstActivated)
                _burstFallbacks.Remove(uid);

            if (gun.NextFire > Timing.CurTime)
            {
                if (_burstFallbacks.TryGetValue(uid, out var pending) && pending.NextFire != gun.NextFire)
                    _burstFallbacks.Remove(uid);

                continue;
            }

            if (TryComp(uid, out AutoShootGunComponent? autoShoot))
            {
                _burstFallbacks.Remove(uid);

                if (!autoShoot.Enabled)
                    continue;

                AttemptShoot((uid, gun));
            }
            else if (gun.BurstActivated)
            {
                // Give an active player's predicted request time to arrive before
                // falling back to authoritative continuation. NPCs, sentries, and
                // dropped/switched weapons do not need the network grace period.
                if (_rmcGun.TryGetGunUser(uid, out var user) &&
                    TryComp(user, out ActorComponent? actor) &&
                    TryGetGun(user, out var activeGun) &&
                    activeGun.Owner == uid)
                {
                    if (!_burstFallbacks.TryGetValue(uid, out var pending) || pending.NextFire != gun.NextFire)
                    {
                        var ping = Math.Clamp(
                            (int) actor.PlayerSession.Channel.Ping,
                            0,
                            MaxBurstFallbackPingMilliseconds);
                        var tickGrace = TimeSpan.FromTicks(Timing.TickPeriod.Ticks * BurstFallbackGraceTicks);
                        pending = new BurstFallback(
                            gun.NextFire,
                            gun.NextFire + TimeSpan.FromMilliseconds(ping) + tickGrace);
                        _burstFallbacks[uid] = pending;
                    }

                    if (pending.FireAt > Timing.CurTime)
                        continue;
                }

                _burstFallbacks.Remove(uid);

                var parent = TransformSystem.GetParentUid(uid);
                if (HasComp<DamageableComponent>(parent))
                    AttemptShoot(parent, (uid, gun), gun.ShootCoordinates ?? new EntityCoordinates(uid, gun.DefaultDirection));
                else
                    AttemptShoot((uid, gun));
            }
        }
    }

    private readonly record struct BurstFallback(TimeSpan NextFire, TimeSpan FireAt);
}
