using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Attachable.Events;
using Content.Shared._RMC14.Attachable.Systems;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._AU14.Weapons.GunSwitch;

/// <summary>
///     Runs the "Switch" auto-sear chip: rate-of-fire and accuracy changes while the chip's toggle
///     is engaged, the per-shot jam roll, and the rack-to-clear interaction on a jammed gun.
/// </summary>
public sealed class GunSwitchSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;

    // Blow-up presentation: a puff of fog + sparks + a metal-break bang where the gun was.
    private static readonly EntProtoId ExplodeFogEffect = "AU14CollapseFog";
    private static readonly EntProtoId ExplodeSparkEffect = "EffectSparks";
    private static readonly SoundSpecifier ExplodeSound = new SoundCollectionSpecifier("MetalBreak");
    private const string ExplodeDamageType = "Piercing";

    // The dump self-terminates when the gun hasn't managed to fire for this long (ran dry, jammed...).
    private static readonly TimeSpan DumpTimeout = TimeSpan.FromSeconds(0.75);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunSwitchComponent, AttachableRelayedEvent<GunRefreshModifiersEvent>>(OnRefreshModifiers);
        SubscribeLocalEvent<GunSwitchComponent, AttachableRelayedEvent<GetWeaponAccuracyEvent>>(OnGetAccuracy);
        SubscribeLocalEvent<GunSwitchComponent, AttachableRelayedEvent<GunShotEvent>>(OnGunShot);

        SubscribeLocalEvent<GunJammedComponent, AttemptShootEvent>(OnJammedShootAttempt);
        SubscribeLocalEvent<GunJammedComponent, GetVerbsEvent<InteractionVerb>>(OnJammedGetVerbs);
        SubscribeLocalEvent<GunJammedComponent, GunJamRackDoAfterEvent>(OnRacked);
    }

    // The chip counts as engaged while its toggle action (the "Switch" fire mode) is active.
    private bool Engaged(EntityUid chip) =>
        TryComp(chip, out AttachableToggleableComponent? toggle) && toggle.Active;

    private void OnRefreshModifiers(Entity<GunSwitchComponent> ent, ref AttachableRelayedEvent<GunRefreshModifiersEvent> args)
    {
        if (!Engaged(ent))
            return;

        args.Args.FireRate *= ent.Comp.FireRateMultiplier;
        // "Reduced accuracy" also means a wider scatter cone at both ends.
        args.Args.MinAngle *= ent.Comp.ScatterMultiplier;
        args.Args.MaxAngle *= ent.Comp.ScatterMultiplier;
    }

    private void OnGetAccuracy(Entity<GunSwitchComponent> ent, ref AttachableRelayedEvent<GetWeaponAccuracyEvent> args)
    {
        if (Engaged(ent))
            args.Args.AccuracyMultiplier *= ent.Comp.AccuracyMultiplier;
    }

    private void OnGunShot(Entity<GunSwitchComponent> ent, ref AttachableRelayedEvent<GunShotEvent> args)
    {
        // All rolls are server-only so prediction can't desync them; the networked jam component
        // carries the state to clients.
        if (_net.IsClient || !Engaged(ent) || HasComp<GunJammedComponent>(args.Holder))
            return;

        var gun = args.Holder;
        var user = args.Args.User;

        // Catastrophic failure: the gun vanishes in fog and sparks and bites the wielder.
        if (_random.Prob(ent.Comp.ExplodeChance))
        {
            // Plain params: the chip's component must not be wrapped in an Entity<> keyed on the
            // GUN's uid (that owner mismatch tripped a fatal debug assert).
            Explode(gun, ent.Comp, user);
            return;
        }

        if (_random.Prob(ent.Comp.JamChance))
        {
            EnsureComp<GunJammedComponent>(gun);
            RemComp<GunSwitchDumpingComponent>(gun);
            _popup.PopupEntity(Loc.GetString("au14-switch-jammed"), gun, user, PopupType.MediumCaution);
            return;
        }

        // One shot fired while engaged commits the gun to a full dump: it keeps firing on its own
        // even after the trigger is released, until it runs dry, jams, blows up, or leaves the hand.
        var dump = EnsureComp<GunSwitchDumpingComponent>(gun);
        dump.User = user;
        dump.Target = args.Args.ToCoordinates;
        dump.LastShot = _timing.CurTime;
    }

    private void Explode(EntityUid gun, GunSwitchComponent chip, EntityUid user)
    {
        var coords = Transform(gun).Coordinates;

        Spawn(ExplodeFogEffect, coords);
        Spawn(ExplodeSparkEffect, coords);
        _audio.PlayPvs(ExplodeSound, coords);

        var damage = new DamageSpecifier();
        damage.DamageDict[ExplodeDamageType] = FixedPoint2.New(chip.ExplodeDamage);
        _damageable.TryChangeDamage(user, damage, origin: gun);

        _popup.PopupEntity(Loc.GetString("au14-switch-exploded"), user, user, PopupType.LargeCaution);
        QueueDel(gun);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<GunSwitchDumpingComponent, GunComponent>();
        while (query.MoveNext(out var uid, out var dump, out var gunComp))
        {
            // Stop when the shooter is gone, let go of the gun, or the gun stopped producing shots
            // (dry, jam, chip detached/toggled off - anything that keeps GunShotEvent from
            // refreshing LastShot).
            if (Deleted(dump.User) || !_hands.IsHolding(dump.User, uid) || now - dump.LastShot > DumpTimeout)
            {
                RemComp<GunSwitchDumpingComponent>(uid);
                continue;
            }

            // AttemptShoot respects the gun's own fire-rate cooldown, so calling it every tick just
            // sustains fire at the (multiplied) rate - the involuntary mag dump.
            _gun.AttemptShoot(dump.User, uid, gunComp, dump.Target);
        }
    }

    private void OnJammedShootAttempt(Entity<GunJammedComponent> ent, ref AttemptShootEvent args)
    {
        args.Cancelled = true;
        args.Message = Loc.GetString("au14-switch-jammed-shoot");
    }

    private void OnJammedGetVerbs(Entity<GunJammedComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("au14-switch-rack-verb"),
            Act = () => StartRack(ent, user),
        });
    }

    private void StartRack(Entity<GunJammedComponent> ent, EntityUid user)
    {
        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.RackTime, new GunJamRackDoAfterEvent(), ent, ent)
        {
            NeedHand = true,
            BreakOnMove = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnRacked(Entity<GunJammedComponent> ent, ref GunJamRackDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || _net.IsClient)
            return;

        args.Handled = true;

        // Racking is deliberately unreliable: most pulls fail and have to be repeated.
        if (_random.Prob(ent.Comp.RackFailChance))
        {
            _popup.PopupEntity(Loc.GetString("au14-switch-rack-fail"), ent, args.User, PopupType.SmallCaution);
            return;
        }

        RemComp<GunJammedComponent>(ent);
        _popup.PopupEntity(Loc.GetString("au14-switch-rack-success"), ent, args.User);
    }
}
