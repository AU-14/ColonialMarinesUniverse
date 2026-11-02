using System.Numerics;
using Content.Shared._RMC14.Sentry;
using Content.Shared._RMC14.Vents;
using Content.Shared._RMC14.Xenonids.Burrow;
using Content.Shared.CMU14.Fob;
using Content.Shared.CMU14.Sentry;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Fob;

/// <summary>Two deployed, finite-ammunition defenses per bank. Every shot rechecks reserve and counterplay.</summary>
public sealed partial class CMUFobDefenseSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private CMUFobInteractionSystem _controls = default!;
    [Dependency] private CMUFobEquipmentSystem _equipment = default!;
    [Dependency] private CMUFobEffectsSystem _effects = default!;
    [Dependency] private SharedGunSystem _guns = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private CMUFobReserveSystem _reserve = default!;
    [Dependency] private SentrySystem _sentry = default!;
    [Dependency] private SharedSentryTargetingSystem _targeting = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly SoundSpecifier Warning = new SoundPathSpecifier("/Audio/Effects/double_beep.ogg");
    private readonly HashSet<Entity<MobStateComponent>> _candidates = new();
    private EntityUid? _firing;
    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUFobDefenseNetworkComponent, CMUFobReserveChangedEvent>(OnReserveChanged);
        SubscribeLocalEvent<CMUFobSupportGunComponent, CMUFobFireAuthorizationEvent>(OnAuthorizeFire);
        SubscribeLocalEvent<CMUFobSupportGunComponent, CMUSentryBrokenEvent>(OnBroken);
        SubscribeLocalEvent<CMUFobSupportGunComponent, CMUSentryRepairedEvent>(OnRepaired);
        SubscribeLocalEvent<CMUFobSupportGunComponent, CMUSentryStateUpdatedEvent>(OnSentryStateUpdated);
        SubscribeLocalEvent<CMUFobSupportGunComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CMUFobSupportGunComponent, Content.Shared.Damage.Systems.DamageChangedEvent>(OnGunDamage);
        InitializeInteractions();
        InitializeModules();
    }

    /// <summary>Developer fixture setup. Live deployment registers the delivered platforms through TryDeployPlatform.</summary>
    public bool TryRegisterMounts(Entity<CMUFobNetworkComponent> network, EntityUid first, EntityUid second)
    {
        if (!_reserve.Enabled || MetaData(network).EntityPaused
            || network.Comp.State is not (CMUFobReserveState.Standby or CMUFobReserveState.Empty)
            || !_reserve.TryGetNetwork(network.Comp.Faction, out var registered) || registered.Owner != network.Owner
            || HasComp<CMUFobDefenseNetworkComponent>(network) || first == second
            || !CanRegister(first, network, out var a) || !CanRegister(second, network, out var b))
            return false;

        var defense = AddComp<CMUFobDefenseNetworkComponent>(network);
        defense.Mounts.Add(first);
        defense.Mounts.Add(second);
        RegisterMount((first, a), network);
        RegisterMount((second, b), network);
        return true;
    }

    private bool CanRegister(EntityUid uid, Entity<CMUFobNetworkComponent> network, out CMUFobGunMountComponent mount)
    {
        return TryComp(uid, out mount!) && mount.Network == null && !TerminatingOrDeleted(uid)
            && !MetaData(uid).EntityPaused && Transform(uid).Anchored && Transform(uid).MapUid == network.Comp.Map;
    }

    private void RegisterMount(Entity<CMUFobGunMountComponent> mount, Entity<CMUFobNetworkComponent> network)
    {
        mount.Comp.Network = network;
        mount.Comp.Position = _transform.GetWorldPosition(mount);
        mount.Comp.Facing = _transform.GetWorldRotation(mount);
        InstallGun(mount, network, replacement: false);
    }

    private void InstallGun(Entity<CMUFobGunMountComponent> mount, Entity<CMUFobNetworkComponent> network, bool replacement)
    {
        var uid = SpawnAtPosition(mount.Comp.WeaponPrototype, Transform(mount).Coordinates);
        _transform.SetWorldRotation(uid, mount.Comp.Facing);
        mount.Comp.Gun = uid;
        EnsureComp<CMUFobInstructionsComponent>(uid);
        var gun = Comp<CMUFobSupportGunComponent>(uid);
        gun.Mount = mount;
        _appearance.SetData(uid, CMUFobGunVisuals.Installed, true);
        _targeting.TryApplyDefaultFaction(uid, network.Comp.Faction);

        if (!mount.Comp.MagazineIssued)
        {
            mount.Comp.MagazineIssued = true;
            TrySpawnInContainer(gun.MagazinePrototype, uid, "gun_magazine", out _);
        }

        if (!replacement)
            return;
        // Rebuilding uses the normal cable/welder/tool repair loop. No replacement magazine or starter charge.
        EnsureComp<CMUSentryBrokenComponent>(uid);
        var broken = new CMUSentryBrokenEvent();
        RaiseLocalEvent(uid, ref broken);
    }

    private bool TryGetMount(Entity<CMUFobSupportGunComponent> gun,
        out Entity<CMUFobGunMountComponent> mount, out Entity<CMUFobNetworkComponent> network)
    {
        mount = default;
        network = default;
        if (!_reserve.Enabled || TerminatingOrDeleted(gun) || MetaData(gun).EntityPaused
            || gun.Comp.Mount is not { } uid || !TryComp<CMUFobGunMountComponent>(uid, out var comp)
            || comp.Gun != gun.Owner || !ValidMount((uid, comp), out network)
            || !Transform(gun).Anchored || Transform(gun).MapUid != network.Comp.Map
            || Vector2.DistanceSquared(_transform.GetWorldPosition(gun), comp.Position) > 0.01f)
            return false;

        mount = (uid, comp);
        return true;
    }

    private bool ValidMount(Entity<CMUFobGunMountComponent> mount, out Entity<CMUFobNetworkComponent> network)
    {
        network = default;
        if (TerminatingOrDeleted(mount) || MetaData(mount).EntityPaused || mount.Comp.Network is not { } uid
            || !TryComp<CMUFobNetworkComponent>(uid, out var bank) || TerminatingOrDeleted(uid)
            || bank.State == CMUFobReserveState.Closed || MetaData(uid).EntityPaused
            || !TryComp<CMUFobDefenseNetworkComponent>(uid, out var defense) || !defense.Mounts.Contains(mount)
            || !Transform(mount).Anchored || Transform(mount).MapUid != bank.Map
            || Vector2.DistanceSquared(_transform.GetWorldPosition(mount), _transform.GetWorldPosition(uid)) > bank.DeploymentRange * bank.DeploymentRange
            || !_sites.CanPlaceUtility(uid, _transform.GetWorldPosition(mount))
            || Vector2.DistanceSquared(_transform.GetWorldPosition(mount), mount.Comp.Position) > 0.01f)
            return false;
        network = (uid, bank);
        return true;
    }

    private bool CanFire(Entity<CMUFobSupportGunComponent> gun)
    {
        return TryGetMount(gun, out _, out var network) && !gun.Comp.RequiresRestart
            && !HasComp<CMUSentryBrokenComponent>(gun) && !_doAfter.IsRunning(gun.Comp.Reload)
            && _controls.HasOperationalControl(network) && _reserve.IsActive(network)
            && _guns.GetAmmoCount(gun) > 0;
    }

    private bool ValidTarget(Entity<CMUFobSupportGunComponent> gun, EntityUid target)
    {
        if (TerminatingOrDeleted(target) || MetaData(target).EntityPaused
            || !TryComp<MobStateComponent>(target, out var mob) || mob.CurrentState == MobState.Dead
            || Transform(target).MapUid != Transform(gun).MapUid || _containers.IsEntityInContainer(target)
            || HasComp<VentCrawlingComponent>(target)
            || TryComp<XenoBurrowComponent>(target, out var burrow) && burrow.Active
            || !TryComp<SentryTargetingComponent>(gun, out var targeting) || !_targeting.IsValidTarget((gun, targeting), target)
            || gun.Comp.Mount is not { } uid || !TryComp<CMUFobGunMountComponent>(uid, out var mount))
            return false;

        if (gun.Comp.RequiresSensor)
            return mount.Network is { } bank && _equipment.CanSense(bank, target);

        var delta = _transform.GetWorldPosition(target) - _transform.GetWorldPosition(gun);
        if (delta.LengthSquared() > gun.Comp.Range * gun.Comp.Range
            || Math.Abs(Angle.ShortestDistance(mount.Facing, delta.ToWorldAngle()).Theta) > gun.Comp.HalfArc.Theta)
            return false;
        return _interaction.InRangeUnobstructed(gun.Owner, target, gun.Comp.Range,
            CollisionGroup.Impassable | CollisionGroup.BulletImpassable);
    }

    private void OnAuthorizeFire(Entity<CMUFobSupportGunComponent> gun, ref CMUFobFireAuthorizationEvent args)
    {
        args.Authorized = _firing == gun.Owner && args.User == gun.Owner && CanFire(gun)
            && gun.Comp.Target is { } target && _timing.CurTime >= gun.Comp.ReadyAt && ValidTarget(gun, target);
    }

    private void ClearTarget(Entity<CMUFobSupportGunComponent> gun)
    {
        if (gun.Comp.WarningMarker is { } marker && !TerminatingOrDeleted(marker)) QueueDel(marker);
        gun.Comp.WarningMarker = null;
        gun.Comp.Target = null;
        gun.Comp.ReadyAt = TimeSpan.Zero;
        _appearance.SetData(gun, CMUFobGunVisuals.Warning, false);
    }

    private void SetMode(EntityUid uid, SentryMode mode)
    {
        if (TryComp<SentryComponent>(uid, out var sentry))
            _sentry.TrySetMode((uid, sentry), mode);
    }

    private void OnReserveChanged(Entity<CMUFobDefenseNetworkComponent> ent, ref CMUFobReserveChangedEvent args)
    {
        if (!TryComp<CMUFobNetworkComponent>(ent, out var bank))
            return;
        Entity<CMUFobNetworkComponent> network = (ent, bank);
        var defense = ent.Comp;
        if (args.State == CMUFobReserveState.Starting)
        {
            if (defense.WarnedActivation == network.Comp.ActivationGeneration)
                return;
            defense.WarnedActivation = network.Comp.ActivationGeneration;
        }
        foreach (var uid in defense.Mounts)
        {
            if (!TryComp<CMUFobGunMountComponent>(uid, out var mount) || mount.Gun is not { } weapon
                || !TryComp<CMUFobSupportGunComponent>(weapon, out var gun) || TerminatingOrDeleted(weapon))
                continue;
            // Relay awards also notify Active: they must not reset an existing acquisition or rearm a repaired gun.
            if (args.State == CMUFobReserveState.Active)
                continue;
            ClearTarget((weapon, gun));
            SetMode(weapon, SentryMode.Off);
            if (args.State != CMUFobReserveState.Starting || HasComp<CMUSentryBrokenComponent>(weapon)
                || !TryGetMount((weapon, gun), out _, out _))
                continue;
            gun.RequiresRestart = false;
            _appearance.SetData(weapon, CMUFobGunVisuals.Warning, true);
            UpdatePresentation((weapon, gun));
            _audio.PlayPvs(Warning, weapon);
            _popup.PopupEntity(Loc.GetString("cmu-fob-gun-starting", ("seconds", (int) network.Comp.StartupDuration.TotalSeconds)),
                weapon, PopupType.MediumCaution);
        }
    }

    private void OnBroken(Entity<CMUFobSupportGunComponent> gun, ref CMUSentryBrokenEvent args)
    {
        if (_damage.GetTotalDamage(gun.Owner) > 0 && gun.Comp.Mount is { } mount
            && TryComp<CMUFobGunMountComponent>(mount, out var slot) && slot.Network is { } network)
            _reserve.RecordGunUse(network, broken: true);
        gun.Comp.RestoreCollision = false;
        gun.Comp.RequiresRestart = true;
        ClearTarget(gun);
        _doAfter.Cancel(gun.Comp.Reload);
        SetMode(gun, SentryMode.Off);
        SetCollision(gun, false);
        UpdatePresentation(gun);
        _effects.Play(gun, CMUFobPresentationCue.Break);
    }

    private void OnRepaired(Entity<CMUFobSupportGunComponent> gun, ref CMUSentryRepairedEvent args)
    {
        gun.Comp.RequiresRestart = true;
        // The common repair system removes Broken at the end of the tick.
        gun.Comp.RestoreCollision = true;
        ClearTarget(gun);
        SetMode(gun, SentryMode.Off);
        _effects.Play(gun, CMUFobPresentationCue.Repair);
    }

    private void OnSentryStateUpdated(Entity<CMUFobSupportGunComponent> gun, ref CMUSentryStateUpdatedEvent args)
        => SetCollision(gun, !HasComp<CMUSentryBrokenComponent>(gun));

    private void SetCollision(EntityUid uid, bool intact)
    {
        if (!TryComp<FixturesComponent>(uid, out var fixtures) || !fixtures.Fixtures.TryGetValue("sentry", out var fixture))
            return;
        var collision = intact ? (int) (CollisionGroup.MidImpassable | CollisionGroup.BulletImpassable) : 0;
        _physics.SetHard(uid, fixture, intact);
        _physics.SetCollisionLayer(uid, "sentry", fixture, collision, manager: fixtures);
        _physics.SetCollisionMask(uid, "sentry", fixture, collision, manager: fixtures);
    }

    private void OnExamined(Entity<CMUFobSupportGunComponent> gun, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(!_reserve.Enabled ? "cmu-fob-disabled"
            : gun.Comp.RequiresRestart ? "cmu-fob-gun-restart"
            : gun.Comp.HalfArc.Degrees >= 180 ? "cmu-fob-gun-examine" : "cmu-fob-launcher-examine"));
        args.PushMarkup(Loc.GetString("cmu-fob-gun-ammo", ("count", _guns.GetAmmoCount(gun))));
        if (_reserve.Enabled && TryGetMount(gun, out _, out var network))
            args.PushMarkup(_reserve.GetPublicState(network));
    }

    private void UpdatePresentation(Entity<CMUFobSupportGunComponent> gun)
    {
        if (!_reserve.Enabled)
            return;
        var valid = TryGetMount(gun, out var mount, out var bank);
        var status = !valid ? CMUFobPresentationStatus.Off
            : HasComp<CMUSentryBrokenComponent>(gun) ? CMUFobPresentationStatus.Broken
            : _doAfter.IsRunning(gun.Comp.Reload) ? CMUFobPresentationStatus.Working
            : gun.Comp.RequiresRestart ? CMUFobPresentationStatus.Off
            : bank.Comp.State == CMUFobReserveState.Starting ? CMUFobPresentationStatus.Starting
            : _guns.GetAmmoCount(gun) <= 0 ? CMUFobPresentationStatus.Empty
            : !CanFire(gun) ? CMUFobPresentationStatus.Off
            : gun.Comp.Target != null && _timing.CurTime < gun.Comp.ReadyAt ? CMUFobPresentationStatus.Warning
            : CMUFobPresentationStatus.Active;
        _effects.SetStatus(gun, status);
        var aim = gun.Comp.Target is { } target
            ? _transform.GetWorldPosition(target) - _transform.GetWorldPosition(gun) : Vector2.Zero;
        _appearance.SetData(gun, CMUFobPresentationVisuals.Aim, aim.LengthSquared() > 0 ? Vector2.Normalize(aim) : Vector2.Zero);
        if (valid)
            _effects.SetStatus(mount, status);
    }

    public override void Update(float frameTime)
    {
        if (!_reserve.Enabled || _timing.CurTime < _nextUpdate)
            return;
        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(0.1);
        var mounts = EntityQueryEnumerator<CMUFobGunMountComponent>();
        while (mounts.MoveNext(out var uid, out var mount))
        {
            if (mount.Gun == null)
                _effects.SetStatus(uid, CMUFobPresentationStatus.Off);
            if (!mount.RemovedByOperator && mount.Network != null && ValidMount((uid, mount), out var bank)
                && (mount.Gun == null || Deleted(mount.Gun)))
                InstallGun((uid, mount), bank, replacement: true);
        }

        var guns = EntityQueryEnumerator<CMUFobSupportGunComponent, GunComponent>();
        while (guns.MoveNext(out var uid, out var gun, out var weapon))
        {
            Entity<CMUFobSupportGunComponent> ent = (uid, gun);
            if (gun.RestoreCollision && !HasComp<CMUSentryBrokenComponent>(uid))
            {
                gun.RestoreCollision = false;
                SetCollision(uid, true);
            }
            if (!CanFire(ent))
            {
                ClearTarget(ent);
                SetMode(uid, SentryMode.Off);
                // Standard sentry mode changes can harden a fixture; wrecks must remain passable.
                if (HasComp<CMUSentryBrokenComponent>(uid) || gun.Mount == null)
                    SetCollision(uid, false);
                if (TryGetMount(ent, out _, out var bank) && bank.Comp.State == CMUFobReserveState.Starting
                    && !gun.RequiresRestart)
                    _appearance.SetData(uid, CMUFobGunVisuals.Warning, true);
                UpdatePresentation(ent);
                continue;
            }

            SetMode(uid, SentryMode.On);
            if (gun.Target is { } old && !ValidTarget(ent, old))
                ClearTarget(ent);
            if (gun.Target == null)
            {
                _candidates.Clear();
                if (gun.RequiresSensor && TryGetMount(ent, out _, out var sensorNetwork))
                    _equipment.GetSensorCandidates(sensorNetwork, _candidates);
                else
                    _lookup.GetEntitiesInRange(_transform.GetMapCoordinates(uid), gun.Range, _candidates);
                var nearest = float.MaxValue;
                foreach (var candidate in _candidates)
                {
                    if (candidate.Owner == uid || !ValidTarget(ent, candidate))
                        continue;
                    var distance = Vector2.DistanceSquared(_transform.GetWorldPosition(uid), _transform.GetWorldPosition(candidate));
                    if (distance >= nearest)
                        continue;
                    nearest = distance;
                    gun.Target = candidate;
                }

                if (gun.Target == null)
                {
                    UpdatePresentation(ent);
                    continue;
                }
                gun.ReadyAt = _timing.CurTime + gun.AcquisitionDelay;
                gun.WarningMarker = SpawnAtPosition("CMUFobTargetWarning", Transform(gun.Target.Value).Coordinates);
                _effects.SetStatus(gun.WarningMarker.Value, CMUFobPresentationStatus.Warning);
                _appearance.SetData(gun.WarningMarker.Value, CMUFobPresentationVisuals.LockDuration, (float) gun.AcquisitionDelay.TotalSeconds);
                _appearance.SetData(uid, CMUFobGunVisuals.Warning, true);
                _audio.PlayPvs(Warning, uid);
                _popup.PopupEntity(Loc.GetString("cmu-fob-gun-acquiring"), uid, PopupType.MediumCaution);
            }

            if (gun.WarningMarker is { } marker && !TerminatingOrDeleted(marker))
                _transform.SetCoordinates(marker, Transform(gun.Target!.Value).Coordinates);
            UpdatePresentation(ent);
            if (_timing.CurTime < gun.ReadyAt)
                continue;
            _appearance.SetData(uid, CMUFobGunVisuals.Warning, false);
            if (gun.WarningMarker is { } completedMarker && !TerminatingOrDeleted(completedMarker))
                QueueDel(completedMarker);
            gun.WarningMarker = null;
            try
            {
                _firing = uid;
                var before = _guns.GetAmmoCount(uid);
                _guns.AttemptShoot(uid, (uid, weapon), Transform(gun.Target.Value).Coordinates, gun.Target.Value);
                if (before > _guns.GetAmmoCount(uid))
                    _effects.Play(uid, CMUFobPresentationCue.Shot);
                if (TryGetMount(ent, out _, out var network))
                    _reserve.RecordGunUse(network, shots: before - _guns.GetAmmoCount(uid));
                if (gun.RequiresSensor && before > _guns.GetAmmoCount(uid)) ClearTarget(ent);
                UpdatePresentation(ent);
            }
            finally
            {
                _firing = null;
            }
        }
    }
}
