using System.Linq;
using Content.Shared._RMC14.CameraShake;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._RMC14.Vehicle;

public sealed partial class TankCookOffSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private HardpointSystem _hardpoints = default!;
    [Dependency] private VehicleTopologySystem _topology = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private RMCCameraShakeSystem _shake = default!;

    private readonly HashSet<EntityUid> _pending = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<TankCookOffComponent, HardpointIntegrityChangedEvent>(OnIntegrityChanged);
        SubscribeLocalEvent<TankCookOffComponent, VehicleFrameIntegrityChangedEvent>(OnFrameChanged);
        SubscribeLocalEvent<TankCookOffComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRestart);
    }

    private void OnRestart(RoundRestartCleanupEvent args) => _pending.Clear();
    private void OnIntegrityChanged(Entity<TankCookOffComponent> ent, ref HardpointIntegrityChangedEvent args) => _pending.Add(ent);
    private void OnFrameChanged(Entity<TankCookOffComponent> ent, ref VehicleFrameIntegrityChangedEvent args) => _pending.Add(ent);

    private void OnDamageChanged(Entity<TankCookOffComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;

        _pending.Add(ent);
        // The actual projectile is the tool, not its shooter or an unrelated splash explosion.
        if (args.Impact.Delivery == DamageImpactDelivery.Projectile &&
            args.Tool is { } tool && HasComp<TankCookOffProjectileComponent>(tool) &&
            ent.Comp.Enabled && !HasComp<ActiveTankCookOffComponent>(ent) &&
            _random.Prob(Math.Clamp(ent.Comp.CriticalChance, 0, 1)))
            Start(ent);
    }

    private void Start(Entity<TankCookOffComponent> ent)
    {
        if (!ent.Comp.Enabled || HasComp<ActiveTankCookOffComponent>(ent))
            return;
        var active = AddComp<ActiveTankCookOffComponent>(ent);
        active.StartedAt = _timing.CurTime;
        Dirty(ent, active);
    }

    public override void Update(float frameTime)
    {
        // Damage briefly updates individual parts before recomputing hull integrity.
        // Check the settled result, never that transient intermediate zero.
        foreach (var uid in _pending)
        {
            if (TerminatingOrDeleted(uid) || !TryComp<TankCookOffComponent>(uid, out var tank) ||
                !TryComp<HardpointIntegrityComponent>(uid, out var hull))
                continue;
            if (hull.Integrity <= 0)
                Start((uid, tank));
        }
        _pending.Clear();

        var query = EntityQueryEnumerator<ActiveTankCookOffComponent, TankCookOffComponent, HardpointIntegrityComponent>();
        while (query.MoveNext(out var uid, out var active, out var tank, out var hull))
        {
            if (Paused(uid))
                continue;
            var age = (_timing.CurTime - active.StartedAt).TotalSeconds;
            if (!active.Ruptured && age >= tank.RuptureDelay)
            {
                active.Ruptured = true;
                Dirty(uid, active);
                var ruptured = new TankCookOffRupturedEvent();
                RaiseLocalEvent(uid, ref ruptured);
                // A critical ignition gets the same catastrophic result as a destroyed hull.
                foreach (var slot in _topology.GetMountedSlots(uid).ToArray())
                {
                    if (slot.Item is { } item && TryComp<HardpointIntegrityComponent>(item, out var part))
                        _hardpoints.DamageHardpoint(uid, item, part.Integrity);
                }
                _hardpoints.DamageHardpoint(uid, uid, hull.Integrity);
                _audio.PlayPvs(tank.RuptureSound, uid, AudioParams.Default.WithMaxDistance(35));
                _shake.ShakeCamera(Filter.Empty().AddInRange(_transform.GetMapCoordinates(uid), 14), 3, 1);
            }
            // The ruptured ammunition compartment is a permanent wreck, even after the fire subsides.
        }
    }
}
