using System.Numerics;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Attachable.Systems;
using Content.Shared._RMC14.Stamina;
using Content.Shared.Administration.Logs;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Projectiles;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Weapons.Taser;

/// <summary>
/// Runs wired tasers: embeds the probe, holds the wire, and while the trigger is held pushes current into the target,
/// building from a jolt to a collapse to burns and flames. See <see cref="CMUTetherTaserComponent"/>.
/// </summary>
public sealed partial class CMUTetherTaserSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private AttachableHolderSystem _attachableHolder = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedRMCFlammableSystem _flammable = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private RMCStaminaSystem _stamina = default!;
    [Dependency] private StutteringSystem _stutter = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    /// <summary>Seconds of current before the light jolt turns into convulsions.</summary>
    private const float ConvulseAfter = 3f;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUTetherTaserComponent, ShotAttemptedEvent>(OnShotAttempted);
        SubscribeLocalEvent<CMUTetherTaserComponent, GetVerbsEvent<AlternativeVerb>>(OnTaserVerbs);
        SubscribeLocalEvent<CMUTetherTaserComponent, ComponentShutdown>(OnTaserShutdown);

        SubscribeLocalEvent<CMUTaserProbeComponent, ProjectileHitEvent>(OnProbeHit);

        SubscribeLocalEvent<CMUTaserTetheredComponent, GetVerbsEvent<AlternativeVerb>>(OnTetheredVerbs);
        SubscribeLocalEvent<CMUTaserTetheredComponent, CMUTaserProbePullOutDoAfterEvent>(OnPullOut);
    }

    // With the probe in, the trigger works the current instead of firing; the client keeps asking to shoot every
    // frame it's held down, so each attempt here marks the trigger as still held.
    private void OnShotAttempted(Entity<CMUTetherTaserComponent> ent, ref ShotAttemptedEvent args)
    {
        if (ent.Comp.Target == null)
            return;

        args.Cancel();
        ent.Comp.LastTrigger = _timing.CurTime;
    }

    private void OnTaserVerbs(Entity<CMUTetherTaserComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (ent.Comp.Target == null || !args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-taser-verb-reel-in"),
            Priority = -1,
            Act = () =>
            {
                if (_net.IsServer)
                    Detach(ent, Loc.GetString("cmu-taser-reeled-in"), user);
            },
        });
    }

    private void OnTaserShutdown(Entity<CMUTetherTaserComponent> ent, ref ComponentShutdown args)
    {
        if (_net.IsServer)
            Detach(ent, null, null);
    }

    private void OnProbeHit(Entity<CMUTaserProbeComponent> ent, ref ProjectileHitEvent args)
    {
        if (_net.IsClient ||
            !TryComp(ent, out ProjectileComponent? projectile) ||
            projectile.Weapon is not { } gun ||
            !TryComp(gun, out CMUTetherTaserComponent? taser))
        {
            return;
        }

        var target = args.Target;
        if (!HasComp<RMCStaminaComponent>(target) || _mobState.IsDead(target))
        {
            if (projectile.Shooter is { } shooter)
                _popup.PopupEntity(Loc.GetString("cmu-taser-no-purchase", ("target", Identity.Entity(target, EntityManager))), target, shooter);

            return;
        }

        Attach((gun, taser), target, projectile.Shooter);
    }

    private void Attach(Entity<CMUTetherTaserComponent> taser, EntityUid target, EntityUid? shooter)
    {
        Detach(taser, null, null);

        var probe = SpawnAttachedTo(taser.Comp.EmbeddedProbe, new EntityCoordinates(target, Vector2.Zero));

        var now = _timing.CurTime;
        taser.Comp.Probe = probe;
        taser.Comp.Target = target;
        taser.Comp.LastActivity = now;
        taser.Comp.LastTrigger = TimeSpan.Zero;
        taser.Comp.HeldFor = 0;
        taser.Comp.ShockSeconds = 0;
        taser.Comp.NextPulse = now;
        taser.Comp.AnnouncedStage = CMUTaserShockStage.None;
        taser.Comp.NextClick = now;
        Dirty(taser);

        var tethered = EnsureComp<CMUTaserTetheredComponent>(target);
        tethered.Tasers.Add(taser);
        Dirty(target, tethered);

        PopupStage(target, "cmu-taser-probe-hit", PopupType.MediumCaution);

        _adminLog.Add(LogType.Stamina, LogImpact.Medium,
            $"{ToPrettyString(shooter):user} hooked {ToPrettyString(target):target} with a taser probe from {ToPrettyString(taser):tool}");
    }

    /// <summary>Pulls the probe and drops the wire. Shows <paramref name="message"/> to <paramref name="user"/>, if any.</summary>
    public void Detach(Entity<CMUTetherTaserComponent> taser, string? message, EntityUid? user)
    {
        if (taser.Comp.Target is not { } target)
            return;

        if (taser.Comp.Probe is { } probe && !TerminatingOrDeleted(probe))
            QueueDel(probe);

        if (TryComp(target, out CMUTaserTetheredComponent? tethered))
        {
            tethered.Tasers.Remove(taser);
            if (tethered.Tasers.Count == 0)
                RemCompDeferred<CMUTaserTetheredComponent>(target);
            else
                Dirty(target, tethered);
        }

        taser.Comp.Probe = null;
        taser.Comp.Target = null;
        taser.Comp.HeldFor = 0;
        taser.Comp.ShockSeconds = 0;
        taser.Comp.AnnouncedStage = CMUTaserShockStage.None;
        if (!TerminatingOrDeleted(taser) && taser.Comp.LifeStage <= ComponentLifeStage.Running)
            Dirty(taser);

        if (message != null && user != null)
            _popup.PopupEntity(message, user.Value, user.Value);
    }

    private void OnTetheredVerbs(Entity<CMUTaserTetheredComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Tasers.Count == 0)
            return;

        var user = args.User;
        var target = ent.Owner;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-taser-verb-pull-out"),
            Act = () =>
            {
                var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.PullOutDelay, new CMUTaserProbePullOutDoAfterEvent(), target, target)
                {
                    BreakOnMove = true,
                    NeedHand = true,
                };

                _doAfter.TryStartDoAfter(doAfter);
            },
        });
    }

    private void OnPullOut(Entity<CMUTaserTetheredComponent> ent, ref CMUTaserProbePullOutDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        if (_net.IsClient)
            return;

        foreach (var gun in new List<EntityUid>(ent.Comp.Tasers))
        {
            if (TryComp(gun, out CMUTetherTaserComponent? taser))
                Detach((gun, taser), null, null);
        }

        // Detaching the last taser already removes this; only stale entries are left to clear.
        if (ent.Comp.Tasers.Count > 0)
            RemCompDeferred<CMUTaserTetheredComponent>(ent);

        _popup.PopupEntity(Loc.GetString("cmu-taser-pulled-out", ("user", Identity.Entity(args.User, EntityManager))), ent, PopupType.Medium);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUTetherTaserComponent>();
        while (query.MoveNext(out var uid, out var taser))
        {
            if (taser.Target is not { } target)
                continue;

            var ent = (uid, taser);
            if (TerminatingOrDeleted(target) || taser.Probe is not { } probe || TerminatingOrDeleted(probe))
            {
                Detach(ent, null, null);
                continue;
            }

            var shooter = GetWielder(uid);
            if (shooter == null || !InWireReach(shooter.Value, probe, taser.WireLength))
            {
                PopupStage(target, "cmu-taser-wire-snaps", PopupType.Small);
                Detach(ent, null, null);
                continue;
            }

            // An underbarrel taser lets its wire go when its holder switches back to the gun it's mounted on.
            if (HasComp<AttachableToggleableComponent>(uid) &&
                (!_attachableHolder.TryGetInhandSupercedingGun(shooter.Value, out var active, out _) || active != uid))
            {
                Detach(ent, Loc.GetString("cmu-taser-reeled-in"), shooter);
                continue;
            }

            if (_mobState.IsDead(target))
            {
                Detach(ent, Loc.GetString("cmu-taser-target-dead"), shooter);
                continue;
            }

            var held = now - taser.LastTrigger <= taser.HoldWindow;
            if (!held)
            {
                if (taser.HeldFor > 0)
                {
                    taser.HeldFor = 0;
                    taser.LastActivity = now;
                }

                taser.ShockSeconds = MathF.Max(0, taser.ShockSeconds - taser.CooldownPerSecond * frameTime);
                if (now - taser.LastActivity >= taser.IdleRetract)
                    Detach(ent, Loc.GetString("cmu-taser-reeled-in"), shooter);

                continue;
            }

            taser.LastActivity = now;

            // A flat battery does nothing but tell the shooter so, once a pulse.
            if (_battery.GetCharge(uid) <= 0)
            {
                taser.HeldFor = 0;
                if (now >= taser.NextPulse)
                {
                    taser.NextPulse = now + taser.PulseInterval;
                    _popup.PopupEntity(Loc.GetString("cmu-taser-no-charge"), shooter.Value, shooter.Value, PopupType.SmallCaution);
                }

                continue;
            }

            // A fresh squeeze makes them yell, unless they only just did.
            if (taser.HeldFor <= 0 && now >= taser.NextScream)
            {
                taser.NextScream = now + taser.ScreamCooldown;
                var scream = new CMUTaserEmoteEvent(taser.ScreamEmote);
                RaiseLocalEvent(target, ref scream);
            }

            taser.HeldFor += frameTime;
            taser.ShockSeconds += frameTime;

            if (now < taser.NextPulse)
                continue;

            taser.NextPulse = now + taser.PulseInterval;
            Pulse(ent, target, shooter.Value);
        }
    }

    /// <summary>One beat of current: battery, stamina, shakes, and the sparks, clicks, burns or fire that go with it.</summary>
    private void Pulse(Entity<CMUTetherTaserComponent> taser, EntityUid target, EntityUid shooter)
    {
        var comp = taser.Comp;
        var seconds = (float) comp.PulseInterval.TotalSeconds;
        if (!_battery.TryUseCharge(taser.Owner, comp.ChargePerSecond * seconds))
        {
            _popup.PopupEntity(Loc.GetString("cmu-taser-no-charge"), shooter, shooter, PopupType.SmallCaution);
            return;
        }

        var rate = MathF.Min(comp.StaminaPerSecond + comp.StaminaRampPerSecond * comp.HeldFor, comp.MaxStaminaPerSecond);
        _stamina.DoStaminaDamage(target, rate * seconds);

        Convulse(comp, target);

        SpawnAtPosition(comp.SparkEffect, Transform(target).Coordinates);
        if (_timing.CurTime >= comp.NextClick)
        {
            comp.NextClick = _timing.CurTime + comp.ClickInterval;
            _audio.PlayPvs(comp.ClickSound, target);
        }

        var stage = comp.ShockSeconds >= comp.IgniteAfter ? CMUTaserShockStage.Burning
            : comp.ShockSeconds >= comp.SmokeAfter ? CMUTaserShockStage.Smoking
            : comp.ShockSeconds >= ConvulseAfter ? CMUTaserShockStage.Convulsing
            : CMUTaserShockStage.Jolt;

        if (stage >= CMUTaserShockStage.Smoking)
        {
            var heat = new DamageSpecifier(_prototypes.Index(comp.HeatType), FixedPoint2.New(comp.HeatPerSecond * seconds));
            _damageable.TryChangeDamage(target, heat, interruptsDoAfters: false, origin: shooter, tool: taser);
        }

        if (stage == CMUTaserShockStage.Burning && !_flammable.IsOnFire(target))
        {
            _flammable.Ignite((target, null), comp.IgniteIntensity, comp.IgniteDuration, comp.IgniteMaxStacks);

            // The burning scream, voiced for their sex, collapsed or not. Only if it took: someone who can't burn would
            // otherwise be made to scream every pulse.
            if (_flammable.IsOnFire(target))
            {
                _adminLog.Add(LogType.Flammable, LogImpact.High,
                    $"{ToPrettyString(shooter):user} set {ToPrettyString(target):target} on fire by holding the current on {ToPrettyString(taser):tool}");

                var burning = new CMUTaserEmoteEvent(comp.BurningEmote);
                RaiseLocalEvent(target, ref burning);
            }
        }

        if (stage <= comp.AnnouncedStage)
            return;

        comp.AnnouncedStage = stage;
        switch (stage)
        {
            case CMUTaserShockStage.Jolt:
                PopupStage(target, "cmu-taser-stage-jolt", PopupType.Small);
                break;
            case CMUTaserShockStage.Convulsing:
                PopupStage(target, "cmu-taser-stage-convulsing", PopupType.MediumCaution);
                break;
            case CMUTaserShockStage.Smoking:
                PopupStage(target, "cmu-taser-stage-smoking", PopupType.LargeCaution);
                break;
            case CMUTaserShockStage.Burning:
                PopupStage(target, "cmu-taser-stage-burning", PopupType.LargeCaution);
                break;
        }
    }

    /// <summary>
    /// The shakes. They grow with all the current put into the target, and grow wilder the longer this squeeze lasts:
    /// past the first few seconds the target jerks around to face every which way and loses their grip on what they hold.
    /// </summary>
    private void Convulse(CMUTetherTaserComponent comp, EntityUid target)
    {
        var built = Math.Clamp(comp.ShockSeconds / comp.IgniteAfter, 0f, 1f);
        var squeeze = Math.Clamp(comp.HeldFor / comp.ConvulsePeakAfter, 0f, 1f);
        var intensity = MathF.Max(built, squeeze);

        _jitter.DoJitter(target,
            TimeSpan.FromSeconds(1.5),
            true,
            float.Lerp(comp.MinJitterAmplitude, comp.MaxJitterAmplitude, intensity),
            float.Lerp(comp.MinJitterFrequency, comp.MaxJitterFrequency, intensity),
            true);
        _stutter.DoStutter(target, TimeSpan.FromSeconds(2 + 8 * intensity), true);

        if (comp.HeldFor < ConvulseAfter)
            return;

        if (_random.Prob(float.Lerp(comp.MinThrashChance, comp.MaxThrashChance, squeeze)))
            _transform.SetLocalRotation(target, _random.Pick(ThrashFacings));

        if (squeeze >= 0.5f && _random.Prob(comp.DropChance * squeeze))
        {
            foreach (var held in new List<EntityUid>(_hands.EnumerateHeld(target)))
            {
                _hands.TryDrop(target, held, checkActionBlocker: false);
            }
        }
    }

    private static readonly Angle[] ThrashFacings =
    [
        Angle.FromDegrees(0),
        Angle.FromDegrees(90),
        Angle.FromDegrees(180),
        Angle.FromDegrees(270),
    ];

    /// <summary>Shows "{key}-self" to the target and "{key}-others" to everyone else around them.</summary>
    private void PopupStage(EntityUid target, string key, PopupType type)
    {
        var name = Identity.Entity(target, EntityManager);
        _popup.PopupEntity(Loc.GetString(key + "-self"), target, target, type);
        _popup.PopupEntity(Loc.GetString(key + "-others", ("target", name)), target, Filter.PvsExcept(target), true, type);
    }

    /// <summary>
    /// Who's holding the taser, through any guns it's mounted under. Null once it's been put away or dropped.
    /// </summary>
    private EntityUid? GetWielder(EntityUid gun)
    {
        var current = gun;
        for (var depth = 0; depth < 4; depth++)
        {
            if (!_container.TryGetContainingContainer(current, out var container))
                return null;

            var owner = container.Owner;
            if (HasComp<HandsComponent>(owner))
                return _hands.IsHolding(owner, current) ? owner : null;

            current = owner;
        }

        return null;
    }

    private bool InWireReach(EntityUid shooter, EntityUid probe, float length)
    {
        var a = _transform.GetMapCoordinates(shooter);
        var b = _transform.GetMapCoordinates(probe);
        return a.MapId == b.MapId && (a.Position - b.Position).LengthSquared() <= length * length;
    }
}
