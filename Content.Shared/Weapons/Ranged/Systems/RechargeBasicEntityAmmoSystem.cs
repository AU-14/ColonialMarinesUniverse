using Content.Shared.Examine;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared.Weapons.Ranged.Systems;

public sealed partial class RechargeBasicEntityAmmoSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _netManager = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RechargeBasicEntityAmmoComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<RechargeBasicEntityAmmoComponent, ExaminedEvent>(OnExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<RechargeBasicEntityAmmoComponent, BasicEntityAmmoProviderComponent>();

        while (query.MoveNext(out var uid, out var recharge, out var ammo))
        {
            if (ammo.Count is null || ammo.Count == ammo.Capacity || recharge.NextCharge == null)
                continue;

            if (recharge.StrictCooldownBoundary
                ? recharge.NextCharge >= _timing.CurTime
                : recharge.NextCharge > _timing.CurTime)
                continue;

            if (_netManager.IsClient && recharge.RechargeChance < 1f)
                continue;

            if (!_random.Prob(recharge.RechargeChance))
            {
                if (recharge.AdvanceOnFailedRecharge)
                {
                    recharge.NextCharge = recharge.NextCharge.Value + TimeSpan.FromSeconds(recharge.RechargeCooldown);
                    Dirty(uid, recharge);
                }

                continue;
            }

            if (_gun.UpdateBasicEntityAmmoCount(uid, ammo.Count.Value + 1, ammo))
            {
                // We don't predict this because occasionally on client it may not play.
                // PlayPredicted will still be predicted on the client.
                if (_netManager.IsServer)
                    _audio.PlayPvs(recharge.RechargeSound, uid);
            }

            var nextCharge = _timing.CurTime + TimeSpan.FromSeconds(recharge.RechargeCooldown);

            if (ammo.Count == ammo.Capacity)
            {
                recharge.NextCharge = recharge.PreserveCooldownWhenFull
                    ? nextCharge
                    : null;
                Dirty(uid, recharge);
                continue;
            }

            recharge.NextCharge = nextCharge;
            Dirty(uid, recharge);
        }
    }

    private void OnInit(EntityUid uid, RechargeBasicEntityAmmoComponent component, MapInitEvent args)
    {
        component.NextCharge = component.StartWithCooldown
            ? _timing.CurTime + TimeSpan.FromSeconds(component.RechargeCooldown)
            : _timing.CurTime;
        Dirty(uid, component);
    }

    private void OnExamined(EntityUid uid, RechargeBasicEntityAmmoComponent component, ExaminedEvent args)
    {
        if (!component.ShowExamineText)
            return;

        if (!TryComp<BasicEntityAmmoProviderComponent>(uid, out var ammo)
            || ammo.Count == ammo.Capacity ||
            component.NextCharge == null)
        {
            args.PushMarkup(Loc.GetString("recharge-basic-entity-ammo-full"));
            return;
        }

        var timeLeft = component.NextCharge + _metadata.GetPauseTime(uid) - _timing.CurTime;
        args.PushMarkup(Loc.GetString("recharge-basic-entity-ammo-can-recharge", ("seconds", Math.Round(timeLeft.Value.TotalSeconds, 1))));
    }

    public void Reset(EntityUid uid, RechargeBasicEntityAmmoComponent? recharge = null)
    {
        if (!Resolve(uid, ref recharge, false))
            return;

        if (recharge.NextCharge == null ||
            recharge.ResetOverdueCooldown && recharge.NextCharge < _timing.CurTime)
        {
            recharge.NextCharge = _timing.CurTime + TimeSpan.FromSeconds(recharge.RechargeCooldown);
            Dirty(uid, recharge);
        }
    }
}
