using System.Numerics;
using Content.Shared._RMC14.Weapons.Common;
using Content.Shared.Body;
using Content.Shared.Camera;
using Content.Shared.CMU14.Weapons.Gauss;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Popups;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Weapons.Gauss;

/// <summary>
///     Runs the M60 gauss gun's charged shot: the unique action spins up a charge, the trigger releases it
///     once full, and a charge held past the overcharge limit discharges by itself with a chance to burn or
///     gib the person holding the gun.
/// </summary>
public sealed class CMUGaussChargeSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedCameraRecoilSystem _cameraRecoil = default!;
    [Dependency] private CMUGaussVaporizeSystem _vaporize = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private CMUGaussGunSystem _gauss = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private ItemToggleSystem _itemToggle = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUGaussGunComponent, UniqueActionEvent>(OnUniqueAction);
        SubscribeLocalEvent<CMUGaussGunComponent, CMUGaussReleaseChargeEvent>(OnReleaseCharge);
        SubscribeLocalEvent<CMUGaussGunComponent, ComponentShutdown>(OnShutdown);
        SubscribeNetworkEvent<CMUGaussAimMessage>(OnAimMessage);
    }

    private void OnAimMessage(CMUGaussAimMessage msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user ||
            !TryGetEntity(msg.Gun, out var gun) ||
            !TryComp(gun, out CMUGaussGunComponent? comp) ||
            comp.ChargeStart == null ||
            comp.ChargeUser != user)
        {
            return;
        }

        comp.AimTarget = msg.Target;
    }

    private void OnShutdown(Entity<CMUGaussGunComponent> ent, ref ComponentShutdown args)
    {
        StopSounds(ent.Comp);
    }

    private void OnUniqueAction(Entity<CMUGaussGunComponent> ent, ref UniqueActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var user = args.UserUid;

        // A building charge can't be vented; it has to be fired.
        if (ent.Comp.ChargeStart != null)
        {
            _popup.PopupEntity(Loc.GetString("cmu-gauss-must-fire"), ent, user, PopupType.SmallCaution);
            return;
        }

        if (!_itemToggle.IsActivated(ent.Owner))
        {
            _popup.PopupEntity(Loc.GetString("cmu-gauss-not-active", ("gun", ent)), ent, user, PopupType.SmallCaution);
            return;
        }

        // It only fires wielded, and can't be unwielded once charging, so it has to start wielded.
        if (TryComp(ent, out WieldableComponent? wieldable) && !wieldable.Wielded)
        {
            _popup.PopupEntity(Loc.GetString("cmu-gauss-must-wield"), ent, user, PopupType.SmallCaution);
            return;
        }

        if (!TryComp(ent, out BatteryAmmoProviderComponent? battery) || battery.Shots < ent.Comp.ChargedShotCost)
        {
            _popup.PopupEntity(Loc.GetString("cmu-gauss-no-charge"), ent, user, PopupType.SmallCaution);
            return;
        }

        var now = _timing.CurTime;
        ent.Comp.ChargeStart = now;
        ent.Comp.ChargeUser = user;
        ent.Comp.FullyCharged = false;
        Dirty(ent);

        ent.Comp.SpinupStream = _audio.PlayPvs(ent.Comp.SpinupSound, ent)?.Entity;
        ent.Comp.LoopStartAt = now + _audio.GetAudioLength(_audio.ResolveSound(ent.Comp.SpinupSound));
        _popup.PopupEntity(Loc.GetString("cmu-gauss-charge-start"), ent, user);
    }

    private void OnReleaseCharge(Entity<CMUGaussGunComponent> ent, ref CMUGaussReleaseChargeEvent args)
    {
        StopCharge(ent);

        if (!TryComp(ent, out BatteryAmmoProviderComponent? battery) || battery.Shots < ent.Comp.ChargedShotCost)
        {
            _popup.PopupEntity(Loc.GetString("cmu-gauss-no-charge"), ent, args.User, PopupType.SmallCaution);
            return;
        }

        _gun.TakeCharge((ent, battery), ent.Comp.ChargedShotCost);
        Discharge(ent, args.User, _transform.ToMapCoordinates(args.Target), ent.Comp.ChargedShot, ent.Comp.ChargedRecoil);
    }

    private void Overcharge(Entity<CMUGaussGunComponent> ent, EntityUid user)
    {
        // The gun lets go on its own at the holder's crosshair, or straight ahead if their client never
        // reported one. It drains what it can, even if that's less than a full charged shot.
        var aim = ent.Comp.AimTarget;
        StopCharge(ent);

        if (TryComp(ent, out BatteryAmmoProviderComponent? battery) && battery.Shots > 0)
            _gun.TakeCharge((ent, battery), Math.Min(battery.Shots, ent.Comp.ChargedShotCost));

        _audio.PlayPvs(ent.Comp.OverchargedSound, ent);
        Discharge(ent, user, aim, ent.Comp.OverchargedShot, ent.Comp.OverchargedRecoil);
        _popup.PopupEntity(Loc.GetString("cmu-gauss-overcharge", ("gun", ent)), user, PopupType.LargeCaution);

        // Vaporizing replaces the burn, so no pain scream gets in ahead of their last scream.
        if (_random.Prob(ent.Comp.GibChance) && HasComp<BodyComponent>(user))
        {
            _vaporize.Start(user, ent);
            return;
        }

        if (_random.Prob(ent.Comp.SelfBurnChance))
        {
            _damageable.TryChangeDamage(user, ent.Comp.SelfBurnDamage, origin: ent);
            _popup.PopupEntity(Loc.GetString("cmu-gauss-overcharge-burn", ("gun", ent)), user, user, PopupType.MediumCaution);
        }
    }

    /// <summary>
    ///     Fires a charged beam from the user toward <paramref name="target"/>, or the way they're facing
    ///     without one, then plays the shot and kicks their camera.
    /// </summary>
    private void Discharge(Entity<CMUGaussGunComponent> gun, EntityUid user, MapCoordinates? target, EntProtoId shot, float recoil)
    {
        var from = Transform(user).Coordinates;
        var userPos = _transform.GetMapCoordinates(user);
        var direction = target is { } aim && aim.MapId == userPos.MapId
            ? aim.Position - userPos.Position
            : Vector2.Zero;

        if (direction.LengthSquared() < 0.0001f)
            direction = _transform.GetWorldRotation(user).ToWorldVec();

        direction = direction.Normalized();

        gun.Comp.ShotAim = target;
        var beam = Spawn(shot, from);
        var ev = new HitscanTraceEvent
        {
            FromCoordinates = from,
            ShotDirection = direction,
            Gun = gun,
            Shooter = user,
        };
        RaiseLocalEvent(beam, ref ev);
        Del(beam);

        _audio.PlayPvs(gun.Comp.ChargedFireSound, gun);
        var scalar = recoil * _gauss.GetRecoilFraction(gun.Comp, user);
        if (scalar > 0)
            _cameraRecoil.KickCamera(user, direction * 0.5f * scalar);
    }

    private void StopCharge(Entity<CMUGaussGunComponent> ent)
    {
        StopSounds(ent.Comp);
        ent.Comp.FullyCharged = false;
        ent.Comp.LoopStartAt = null;
        ent.Comp.AimTarget = null;

        if (ent.Comp.ChargeStart == null && ent.Comp.ChargeUser == null)
            return;

        ent.Comp.ChargeStart = null;
        ent.Comp.ChargeUser = null;
        Dirty(ent);
    }

    private void StopSounds(CMUGaussGunComponent comp)
    {
        comp.SpinupStream = _audio.Stop(comp.SpinupStream);
        comp.LoopStream = _audio.Stop(comp.LoopStream);
        comp.OverchargingStream = _audio.Stop(comp.OverchargingStream);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUGaussGunComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.ChargeStart is not { } start)
                continue;

            var ent = (uid, comp);
            if (comp.ChargeUser is not { } user ||
                TerminatingOrDeleted(user) ||
                !_hands.IsHolding(user, uid) ||
                !_itemToggle.IsActivated(uid))
            {
                StopCharge(ent);
                continue;
            }

            if (comp.LoopStream == null && comp.LoopStartAt is { } loopAt && now >= loopAt)
            {
                comp.LoopStream = _audio.PlayPvs(comp.LoopSound, uid, AudioParams.Default.WithLoop(true))?.Entity;
                comp.LoopStartAt = null;
            }

            var elapsed = now - start;
            if (!comp.FullyCharged && elapsed >= comp.ChargeTime)
            {
                comp.FullyCharged = true;
                comp.OverchargingStream = _audio.PlayPvs(comp.OverchargingSound, uid)?.Entity;
                _popup.PopupEntity(Loc.GetString("cmu-gauss-charged"), uid, user, PopupType.Medium);
            }

            if (elapsed >= comp.OverchargeTime)
                Overcharge(ent, user);
        }
    }
}
