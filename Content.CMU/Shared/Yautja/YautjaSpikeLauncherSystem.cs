using Content.Shared.Examine;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Yautja;

public sealed partial class YautjaSpikeLauncherSystem : EntitySystem
{
    private const string NonYautjaExamineText = "cmu-yautja-spike-launcher-nonyautja-examine";

    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaSpikeLauncherComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<YautjaSpikeLauncherComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<YautjaSpikeLauncherComponent, TakeAmmoEvent>(OnTakeAmmo, after: [typeof(SharedGunSystem)]);
        SubscribeLocalEvent<YautjaSpikeLauncherComponent, AmmoShotEvent>(OnAmmoShot);
        SubscribeLocalEvent<YautjaSpikeLauncherProjectileRefundComponent, EntityTerminatingEvent>(OnProjectileTerminating);
    }

    private void OnMapInit(Entity<YautjaSpikeLauncherComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextCharge = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.RechargeCooldown);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<YautjaSpikeLauncherComponent, BasicEntityAmmoProviderComponent>();
        while (query.MoveNext(out var uid, out var launcher, out var ammo))
        {
            if (ammo.Count is not { } count || count == ammo.Capacity ||
                launcher.NextCharge >= _timing.CurTime || !_random.Prob(launcher.RechargeChance))
                continue;

            // A failed roll retries next tick. Firing and reaching capacity do
            // not reset the last successful regeneration time.
            if (_gun.UpdateBasicEntityAmmoCount((uid, ammo), count + 1))
                _audio.PlayPvs(launcher.RechargeSound, uid);

            launcher.NextCharge = _timing.CurTime + TimeSpan.FromSeconds(launcher.RechargeCooldown);
        }
    }

    private void OnExamined(Entity<YautjaSpikeLauncherComponent> ent, ref ExaminedEvent args)
    {
        if (!HasComp<YautjaComponent>(args.Examiner))
        {
            args.ReplaceDescription(Loc.GetString(NonYautjaExamineText));
            return;
        }

        if (!TryComp(ent, out BasicEntityAmmoProviderComponent? ammo) ||
            ammo.Count is not { } count ||
            ammo.Capacity is not { } capacity)
        {
            return;
        }

        args.PushMarkup(Loc.GetString(
            "cmu-yautja-spike-launcher-examine-spikes",
            ("count", count),
            ("capacity", capacity)));
    }

    private void OnTakeAmmo(Entity<YautjaSpikeLauncherComponent> ent, ref TakeAmmoEvent args)
    {
        foreach (var (ammoEntity, _) in args.Ammo)
        {
            if (ammoEntity is { } uid)
                AddProjectileRefund(uid, ent.Owner);
        }
    }

    private void OnAmmoShot(Entity<YautjaSpikeLauncherComponent> ent, ref AmmoShotEvent args)
    {
        foreach (var projectile in args.FiredProjectiles)
        {
            if (!TryComp(projectile, out YautjaSpikeLauncherProjectileRefundComponent? refund) ||
                refund.Launcher != ent.Owner)
            {
                continue;
            }

            refund.Fired = true;
        }
    }

    private void OnProjectileTerminating(Entity<YautjaSpikeLauncherProjectileRefundComponent> ent, ref EntityTerminatingEvent args)
    {
        if (ent.Comp.Fired ||
            TerminatingOrDeleted(ent.Comp.Launcher) ||
            !HasComp<YautjaSpikeLauncherComponent>(ent.Comp.Launcher) ||
            !TryComp(ent.Comp.Launcher, out BasicEntityAmmoProviderComponent? ammo))
        {
            return;
        }

        _gun.ChangeBasicEntityAmmoCount((ent.Comp.Launcher, ammo), 1);
    }

    private void AddProjectileRefund(EntityUid projectile, EntityUid launcher)
    {
        var refund = EnsureComp<YautjaSpikeLauncherProjectileRefundComponent>(projectile);
        refund.Launcher = launcher;
        refund.Fired = false;
    }
}
