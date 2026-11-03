using Content.Shared.CMU14.Fob;
using Content.Shared.CMU14.Sentry;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;

namespace Content.Server.CMU14.Fob;

public sealed partial class CMUFobDefenseSystem
{
    [Dependency] private DamageableSystem _damage = default!;

    public CMUFobGunStatus[] GetGunStatus(EntityUid network)
    {
        var result = new[] { new CMUFobGunStatus(CMUFobGunCondition.Missing, 0, 0), new CMUFobGunStatus(CMUFobGunCondition.Missing, 0, 0) };
        if (!_reserve.Enabled || !TryComp<CMUFobDefenseNetworkComponent>(network, out var defense))
            return result;
        for (var i = 0; i < Math.Min(2, defense.Mounts.Count); i++)
        {
            if (!TryComp<CMUFobGunMountComponent>(defense.Mounts[i], out var mount) || mount.Gun is not { } uid
                || TerminatingOrDeleted(uid) || !TryComp<CMUFobSupportGunComponent>(uid, out var gun))
                continue;
            var ammo = _guns.GetAmmoCount(uid);
            var condition = !TryGetMount((uid, gun), out _, out var bank) ? CMUFobGunCondition.Displaced
                : HasComp<CMUSentryBrokenComponent>(uid) ? CMUFobGunCondition.Broken
                : _doAfter.IsRunning(gun.Reload) ? CMUFobGunCondition.Reloading
                : gun.RequiresRestart ? CMUFobGunCondition.RestartRequired
                : ammo <= 0 ? CMUFobGunCondition.Empty
                : bank.Comp.State == CMUFobReserveState.Starting ? CMUFobGunCondition.Starting
                : bank.Comp.State == CMUFobReserveState.Active && _controls.HasOperationalControl(network) ? CMUFobGunCondition.Active
                : CMUFobGunCondition.Standby;
            result[i] = new CMUFobGunStatus(condition, ammo, (int) _damage.GetTotalDamage(uid));
        }
        return result;
    }

    private void OnGunDamage(Entity<CMUFobSupportGunComponent> gun, ref DamageChangedEvent args)
    {
        if (args.DamageIncreased && args.DamageDelta != null && gun.Comp.Mount is { } mount
            && TryComp<CMUFobGunMountComponent>(mount, out var slot) && slot.Network is { } network)
            _reserve.RecordGunUse(network, damage: args.DamageDelta.GetTotal().Double());
    }
}
