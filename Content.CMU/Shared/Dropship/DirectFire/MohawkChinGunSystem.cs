using System.Numerics;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.Sentry;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared.CMU14.Dropship.TacticalLand;
using Content.Shared.Eye;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Physics;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Dropship.DirectFire;

public sealed partial class MohawkChinGunSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private GunshipDirectFireSystem _directFire = default!;
    [Dependency] private SharedDropshipSystem _dropships = default!;
    [Dependency] private SharedEyeSystem _eyes = default!;
    [Dependency] private SharedGunSystem _guns = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private SharedSentryTargetingSystem _targeting = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private SharedDropshipWeaponSystem _weapons = default!;
    private readonly HashSet<Entity<MobStateComponent>> _nearbyMobs = new();

    public override void Initialize()
    {
        Subs.BuiEvents<DropshipTerminalWeaponsComponent>(DropshipTerminalWeaponsUi.Key,
            subs =>
            {
                subs.Event<MohawkChinGunControlMessage>(OnControl);
                subs.Event<MohawkChinGunFireMessage>(OnFire);
                subs.Event<MohawkChinGunTrackMessage>(OnTrack);
                subs.Event<MohawkChinGunConfirmTrackingMessage>(OnConfirmTracking);
                subs.Event<MohawkChinGunClearTrackingMessage>(OnClearTracking);
                subs.Event<MohawkChinGunAimMessage>(OnAim);
                subs.Event<MohawkChinGunPanMessage>(OnPan);
                subs.Event<MohawkChinGunPriorityMessage>(OnPriority);
                subs.Event<MohawkChinGunAcquireMessage>(OnAcquire);
                subs.Event<MohawkChinGunFlareMessage>(OnFlare);
                subs.Event<MohawkChinGunTriggerMessage>(OnTrigger);
                subs.Event<MohawkChinGunFeedMessage>(OnFeed);
            });
        SubscribeLocalEvent<MohawkChinGunComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnFire(Entity<DropshipTerminalWeaponsComponent> console, ref MohawkChinGunFireMessage args)
    {
        if (_net.IsClient || !console.Comp.Gunnery || console.Comp.ChinGunOperator != args.Actor ||
            !_dropships.TryGetGridDropship(console, out var ship))
            return;

        var coordinates = GetCoordinates(args.Coordinates);
        if (!coordinates.IsValid(EntityManager))
            return;

        foreach (var uid in ship.Comp.AttachmentPoints)
        {
            if (!TryComp(uid, out MohawkChinGunComponent? chin) || chin.Console != console.Owner ||
                !TryGetWeapon(uid, out var weapon) || !TryComp(weapon, out GunComponent? gun) ||
                !CanOperate((uid, chin), args.Actor, weapon, out _))
                continue;

            SetTracking((uid, chin), null);
            chin.PendingManualShot = coordinates;
            FireAt((uid, chin), args.Actor, weapon, gun, coordinates);
            return;
        }
    }

    public bool IsGrounded(EntityUid point)
    {
        return _dropships.TryGetGridDropship(point, out var ship) &&
               !ship.Comp.Crashed &&
               !HasComp<DropshipTacticalHoverComponent>(ship) &&
               (!TryComp(ship, out FTLComponent? ftl) ||
                ftl.State is FTLState.Available or FTLState.Cooldown);
    }

    public bool IsAirborne(EntityUid point)
    {
        return _dropships.TryGetGridDropship(point, out var ship) && !ship.Comp.Crashed &&
               HasComp<DropshipInFlyByComponent>(ship) && !HasComp<DropshipTacticalHoverComponent>(ship) &&
               TryComp(ship, out FTLComponent? ftl) &&
               ftl.State is FTLState.Travelling or FTLState.Arriving;
    }

    private bool IsSeatedOperator(EntityUid point, EntityUid actor)
    {
        if (!TryComp(actor, out BuckleComponent? buckle) ||
            buckle.BuckledTo is not { } seat ||
            !HasComp<MohawkGunnerySeatComponent>(seat))
            return false;

        return _dropships.TryGetGridDropship(point, out var ship) &&
               _dropships.TryGetGridDropship(seat, out var seatShip) &&
               ship.Owner == seatShip.Owner &&
               (!TryComp(actor, out MobStateComponent? mob) || mob.CurrentState == MobState.Alive);
    }

    public bool CanOperate(Entity<MohawkChinGunComponent> point, EntityUid actor, EntityUid weapon,
        out string? message)
    {
        message = null;
        if (!IsGrounded(point) && !IsAirborne(point))
        {
            message = Loc.GetString("cmu-mohawk-gunnery-ground-only");
            return false;
        }

        if (actor == weapon)
            return _net.IsServer && IsGrounded(point) && point.Comp.Operator == null && point.Comp.DefensiveFire;

        if (point.Comp.Operator != actor || !IsSeatedOperator(point, actor))
        {
            message = Loc.GetString("cmu-mohawk-gunnery-seat");
            return false;
        }

        return TryComp(actor, out RemoteWeaponOperatorComponent? remote) &&
               remote.SelectedWeapon == weapon &&
               point.Comp.Console is { } console &&
               _ui.IsUiOpen(console, DropshipTerminalWeaponsUi.Key, actor);
    }

    private void OnControl(Entity<DropshipTerminalWeaponsComponent> console, ref MohawkChinGunControlMessage args)
    {
        if (_net.IsClient || !console.Comp.Gunnery ||
            !_dropships.TryGetGridDropship(console, out var ship))
            return;

        foreach (var uid in ship.Comp.AttachmentPoints)
        {
            if (!TryComp(uid, out MohawkChinGunComponent? chin))
                continue;

            var point = new Entity<MohawkChinGunComponent>(uid, chin);
            if (chin.Operator == args.Actor)
            {
                ReleaseControl(point);
                return;
            }

            string? error = null;
            if (!IsGrounded(uid) && !IsAirborne(uid))
                error = "cmu-mohawk-gunnery-ground-only";
            else if (!IsSeatedOperator(uid, args.Actor))
                error = "cmu-mohawk-gunnery-seat";
            else if (chin.Operator != null ||
                     TryComp(args.Actor, out RemoteWeaponOperatorComponent? activeRemote) &&
                     activeRemote.SelectedWeapon != null)
                error = "cmu-mohawk-gunnery-busy";

            if (error != null)
            {
                _popup.PopupEntity(Loc.GetString(error), console, args.Actor);
                return;
            }

            if (!TryGetWeapon(uid, out var weapon))
            {
                _popup.PopupEntity(Loc.GetString("rmc-dropship-weapons-fire-no-weapon"), console, args.Actor);
                return;
            }

            if (TryComp(weapon, out DropshipWeaponComponent? dropshipWeapon) &&
                dropshipWeapon.Skills != null && !_skills.HasSkills(args.Actor, dropshipWeapon.Skills))
            {
                _popup.PopupEntity(Loc.GetString("rmc-laser-designator-not-skilled"), console, args.Actor);
                return;
            }

            chin.Operator = args.Actor;
            chin.AirborneControl = IsAirborne(uid);
            console.Comp.ChinGunAirborne = chin.AirborneControl;
            console.Comp.ChinGunFlareRadius = chin.FlareRadius;
            if (!chin.AirborneControl)
                console.Comp.Target = null;
            chin.CameraPan = Vector2.Zero;
            var eye = EnsureComp<EyeComponent>(args.Actor);
            chin.Console = console;
            chin.PreviousEye = eye.Target;
            chin.PreviousDrawFov = eye.DrawFov;
            chin.ControlledWeapon = weapon;
            chin.Eye = Spawn(null, GetCameraCoordinates(uid));
            EnsureComp<EyeComponent>(chin.Eye.Value);
            _eyes.SetDrawFov(chin.Eye.Value, false);
            _eyes.SetTarget(args.Actor, chin.Eye, eye);
            _eyes.SetDrawFov(args.Actor, false, eye);
            chin.AddedRemoteOperator = !HasComp<RemoteWeaponOperatorComponent>(args.Actor);
            var remote = EnsureComp<RemoteWeaponOperatorComponent>(args.Actor);
            chin.PreviousPlatform = remote.Platform;
            remote.Platform = Transform(uid).GridUid;
            remote.SelectedWeapon = weapon;
            Dirty(args.Actor, remote);
            console.Comp.ChinGunOperator = args.Actor;
            console.Comp.ScreenOne.Weapon = GetNetEntity(weapon);
            console.Comp.ChinGunPriority = chin.TargetPriority;
            console.Comp.ChinGunEye = chin.Eye;
            Dirty(console);
            Dirty(point);
            return;
        }

        _popup.PopupEntity(Loc.GetString("rmc-dropship-weapons-fire-no-weapon"), console, args.Actor);
    }

    private bool TryGetWeapon(EntityUid point, out EntityUid weapon)
    {
        weapon = default;
        if (!TryComp(point, out DropshipWeaponPointComponent? mount) ||
            !_containers.TryGetContainer(point, mount.WeaponContainerSlotId, out var container))
            return false;
        foreach (var installed in container.ContainedEntities)
        {
            if (!HasComp<GunshipDirectFireWeaponComponent>(installed))
                continue;
            weapon = installed;
            return true;
        }
        return false;
    }

    private EntityCoordinates GetCameraCoordinates(EntityUid point)
    {
        var coords = _transform.GetMapCoordinates(point);
        var rotation = _transform.GetWorldRotation(Transform(point).GridUid ?? point);
        var mount = Comp<GunshipDirectFirePointComponent>(point);
        var chin = Comp<MohawkChinGunComponent>(point);
        var muzzle = coords.Position + rotation.RotateVec(new Vector2(mount.LateralOffset, mount.ForwardOffset));
        var center = muzzle + rotation.RotateVec(Vector2.UnitY * 5f);
        if (chin.Console is { } console && TryComp(console, out DropshipTerminalWeaponsComponent? terminal) &&
            terminal.Target is { } flare && IsUsableFlare((point, chin), flare))
        {
            var anchor = _transform.GetMapCoordinates(flare);
            return _transform.ToCoordinates(new MapCoordinates(anchor.Position + chin.CameraPan, anchor.MapId));
        }
        var delta = center + chin.CameraPan - muzzle;
        var forward = rotation.RotateVec(Vector2.UnitY);
        if (Vector2.Dot(delta, forward) < 0)
            delta -= forward * Vector2.Dot(delta, forward);
        if (delta.LengthSquared() > chin.Range * chin.Range)
            delta = Vector2.Normalize(delta) * chin.Range;
        return _transform.ToCoordinates(new MapCoordinates(muzzle + delta, coords.MapId));
    }

    private void OnShutdown(Entity<MohawkChinGunComponent> point, ref ComponentShutdown args)
    {
        if (_net.IsServer)
            ReleaseControl(point);
    }

    private void ReleaseControl(Entity<MohawkChinGunComponent> point)
    {
        SetTracking(point, null);
        if (point.Comp.Operator is { } actor && !TerminatingOrDeleted(actor))
        {
            if (TryComp(actor, out RemoteWeaponOperatorComponent? remote) &&
                remote.SelectedWeapon == point.Comp.ControlledWeapon)
            {
                if (point.Comp.AddedRemoteOperator)
                    RemComp<RemoteWeaponOperatorComponent>(actor);
                else
                {
                    remote.Platform = point.Comp.PreviousPlatform;
                    remote.SelectedWeapon = null;
                    Dirty(actor, remote);
                }
            }
            if (TryComp(actor, out EyeComponent? eye) && eye.Target == point.Comp.Eye)
            {
                _eyes.SetTarget(actor, point.Comp.PreviousEye is { } previous && !TerminatingOrDeleted(previous)
                    ? previous : null, eye);
                _eyes.SetDrawFov(actor, point.Comp.PreviousDrawFov, eye);
            }
        }

        if (point.Comp.Console is { } console && !TerminatingOrDeleted(console) &&
            TryComp(console, out DropshipTerminalWeaponsComponent? terminal))
        {
            terminal.ChinGunOperator = null;
            terminal.ChinGunEye = null;
            terminal.ChinGunAirborne = false;
            terminal.Target = null;
            Dirty(console, terminal);
        }

        if (point.Comp.Eye is { } camera)
            QueueDel(camera);
        point.Comp.Operator = null;
        point.Comp.Console = null;
        point.Comp.Eye = null;
        point.Comp.PreviousEye = null;
        point.Comp.ControlledWeapon = null;
        point.Comp.PreviousPlatform = null;
        point.Comp.CameraPan = Vector2.Zero;
        point.Comp.HeldAim = null;
        point.Comp.PendingManualShot = null;
        point.Comp.HeldFireUntil = TimeSpan.Zero;
        Dirty(point);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<MohawkChinGunComponent, SentryTargetingComponent>();
        while (query.MoveNext(out var uid, out var chin, out var targeting))
        {
            var point = new Entity<MohawkChinGunComponent>(uid, chin);
            SyncFeedDisplay(point);
            if (TryComp(uid, out GunshipDirectFirePointComponent? directPoint))
            {
                var step = chin.SlewDegreesPerSecond * frameTime;
                var next = directPoint.AimOffsetDegrees + Math.Clamp(chin.DesiredAimDegrees - directPoint.AimOffsetDegrees, -step, step);
                if (next != directPoint.AimOffsetDegrees)
                {
                    directPoint.AimOffsetDegrees = next;
                    _appearance.SetData(uid, GunshipDirectFireVisuals.AimOffsetDegrees, next);
                    Dirty(uid, directPoint);
                }
            }
            if (chin.Operator is { } actor)
            {
                if ((!IsGrounded(uid) && !IsAirborne(uid)) || chin.AirborneControl != IsAirborne(uid) ||
                    !IsSeatedOperator(uid, actor) || chin.Console is not { } console ||
                    !_ui.IsUiOpen(console, DropshipTerminalWeaponsUi.Key, actor) || !TryGetWeapon(uid, out _))
                    ReleaseControl(point);
                else if (chin.Eye is { } eye)
                {
                    if (chin.TrackedTarget is { } target)
                    {
                        if (TerminatingOrDeleted(target) || !EnsureTrackingFaction(point) ||
                            !_targeting.IsValidTarget((uid, targeting), target) ||
                            TryComp(target, out MobStateComponent? targetMob) && targetMob.CurrentState == MobState.Dead ||
                            chin.TrackingFireConfirmed && TryGetWeapon(uid, out var loadedWeapon) &&
                            TryComp(loadedWeapon, out GunshipDirectFireWeaponComponent? loadedDirect) &&
                            _directFire.TryResolveMount((loadedWeapon, loadedDirect), out _, out _, out _, out var payload) &&
                            (payload == null || payload.Rounds <= 0))
                        {
                            SetTracking(point, null);
                            _popup.PopupEntity(Loc.GetString("cmu-dropship-track-lost"), console, actor);
                        }
                        else
                        {
                            if (chin.TrackingFireConfirmed && _timing.CurTime >= chin.AuthorizedUntil)
                            {
                                chin.TrackingFireConfirmed = false;
                                Comp<DropshipTerminalWeaponsComponent>(console).TrackingFireConfirmed = false;
                                Dirty(point);
                                Dirty(console, Comp<DropshipTerminalWeaponsComponent>(console));
                                _popup.PopupEntity(Loc.GetString("cmu-mohawk-gunnery-auth-expired"), console, actor);
                            }
                            if (!CanTrack(point, target))
                                continue;
                            _transform.SetCoordinates(eye, _transform.GetMoverCoordinates(target));
                            if (!chin.AirborneControl && TryGetWeapon(uid, out var aimedWeapon))
                                RequestAim(point, aimedWeapon, _transform.GetMoverCoordinates(target));
                            if (chin.TrackingFireConfirmed && TryGetWeapon(uid, out var trackedWeapon) &&
                                TryComp(trackedWeapon, out GunComponent? trackedGun))
                                FireAt(point, actor, trackedWeapon, trackedGun, Transform(target).Coordinates, target);
                            continue;
                        }
                    }
                    _transform.SetCoordinates(eye, GetCameraCoordinates(uid));
                    if (chin.HeldAim != null && _timing.CurTime >= chin.HeldFireUntil)
                    {
                        chin.HeldAim = null;
                        chin.PendingManualShot = null;
                        chin.HeldFireUntil = TimeSpan.Zero;
                    }
                    var manualAim = _timing.CurTime < chin.HeldFireUntil ? chin.HeldAim : chin.PendingManualShot;
                    if (manualAim is { } pending && TryGetWeapon(uid, out var manualWeapon) &&
                        TryComp(manualWeapon, out GunComponent? manualGun))
                        FireAt(point, actor, manualWeapon, manualGun, pending);
                }
                continue;
            }

            if (!IsGrounded(uid) || _timing.CurTime < chin.NextScan ||
                !TryGetWeapon(uid, out var weapon) || !TryComp(weapon, out GunComponent? gun) ||
                !TryComp(weapon, out GunshipDirectFireWeaponComponent? direct))
                continue;

            chin.NextScan = _timing.CurTime + TimeSpan.FromMilliseconds(250);
            if (!_dropships.TryGetGridFaction(uid, out var faction))
                continue;
            if (!string.Equals(chin.AssignedFaction, faction, StringComparison.OrdinalIgnoreCase))
            {
                if (!_targeting.TryApplyDefaultFaction(uid, faction))
                    continue;
                chin.AssignedFaction = faction;
            }

            if (!_directFire.TryResolveMount((weapon, direct), out var mount, out var grid,
                    out _, out var ammo) || ammo == null || ammo.Rounds <= 0)
                continue;

            var defensive = SelectTarget(point, chin.CloseDefenseRange);
            var selected = defensive ?? SelectTarget(point);
            if (selected is { } hostile)
            {
                RequestAim(point, weapon, Transform(hostile).Coordinates);
                if (defensive != null)
                {
                    chin.DefensiveFire = true;
                    try
                    {
                        _guns.AttemptShoot(weapon, (weapon, gun), Transform(hostile).Coordinates, hostile);
                    }
                    finally
                    {
                        chin.DefensiveFire = false;
                    }
                }
            }
            else
                chin.DesiredAimDegrees = 0f;
        }
    }

    private bool TryGetControlledPoint(Entity<DropshipTerminalWeaponsComponent> console, EntityUid actor,
        out Entity<MohawkChinGunComponent> point)
    {
        point = default;
        if (!console.Comp.Gunnery || console.Comp.ChinGunOperator != actor ||
            !_dropships.TryGetGridDropship(console, out var ship))
            return false;
        foreach (var uid in ship.Comp.AttachmentPoints)
        {
            if (!TryComp(uid, out MohawkChinGunComponent? chin) || chin.Console != console.Owner ||
                !TryGetWeapon(uid, out var weapon) || !CanOperate((uid, chin), actor, weapon, out _))
                continue;
            point = (uid, chin);
            return true;
        }
        return false;
    }

    public bool CanTrack(Entity<MohawkChinGunComponent> point, EntityUid target)
    {
        return CanTrack(point, target, false);
    }

    private bool CanTrack(Entity<MohawkChinGunComponent> point, EntityUid target, bool knownHostile)
    {
        if (TerminatingOrDeleted(target) || (!IsGrounded(point) && !IsAirborne(point)) ||
            !TryComp(point, out SentryTargetingComponent? targeting) ||
            !EnsureTrackingFaction(point) ||
            !TryGetWeapon(point, out var weapon) ||
            !TryComp(weapon, out GunshipDirectFireWeaponComponent? direct) ||
            !_directFire.TryResolveMount((weapon, direct), out var mount, out var grid, out _, out _) ||
            TryComp(target, out MobStateComponent? mob) && mob.CurrentState == MobState.Dead)
            return false;
        if (IsAirborne(point))
            return _targeting.IsValidTarget((point.Owner, targeting), target) &&
                   IsInFlareArea(point, Transform(target).Coordinates) &&
                   _weapons.CanChinGunImpactAt(Transform(target).Coordinates,
                       _directFire.TryGetFeedAmmo(point, out var airborneAmmo) ? airborneAmmo.Comp.ZLevelPenetration : 0);
        var hostile = knownHostile;
        if (!knownHostile)
        {
            foreach (var candidate in _targeting.GetNearbyIffHostiles((point.Owner, targeting), GetScanRange(point)))
            {
                if (candidate == target)
                {
                    hostile = true;
                    break;
                }
            }
        }
        if (!hostile || _dropships.TryGetGridDropship(target, out var targetShip) && targetShip.Owner == grid)
            return false;
        var rotation = _transform.GetWorldRotation(grid);
        var origin = _transform.GetMapCoordinates(point);
        var position = origin.Position + rotation.RotateVec(new Vector2(mount.Comp.LateralOffset, mount.Comp.ForwardOffset));
        var location = _transform.GetMapCoordinates(target);
        var delta = location.Position - position;
        if (location.MapId != origin.MapId || delta.LengthSquared() < 0.0001f ||
            delta.LengthSquared() > point.Comp.Range * point.Comp.Range ||
            Vector2.Dot(rotation.RotateVec(Vector2.UnitY), delta) < -0.0001f ||
            !IsInFlareArea(point, Transform(target).Coordinates))
            return false;
        var muzzle = new MapCoordinates(position + Vector2.Normalize(delta) * direct.MuzzleOffset, origin.MapId);
        return _interaction.InRangeUnobstructed(muzzle, location, point.Comp.Range, CollisionGroup.Opaque,
            predicate: blocker => blocker == target || blocker == point.Owner || blocker == weapon ||
                !TryComp(blocker, out OccluderComponent? occluder) || !occluder.Enabled);
    }

    private void SetTracking(Entity<MohawkChinGunComponent> point, EntityUid? target)
    {
        point.Comp.TrackedTarget = target;
        point.Comp.TrackingFireConfirmed = false;
        point.Comp.AuthorizedUntil = TimeSpan.Zero;
        point.Comp.PendingManualShot = null;
        point.Comp.HeldAim = null;
        point.Comp.HeldFireUntil = TimeSpan.Zero;
        Dirty(point);
        if (point.Comp.Console is { } console && TryComp(console, out DropshipTerminalWeaponsComponent? terminal))
        {
            terminal.TrackedTarget = target;
            terminal.TrackingFireConfirmed = false;
            Dirty(console, terminal);
        }
    }

    private void OnTrack(Entity<DropshipTerminalWeaponsComponent> console, ref MohawkChinGunTrackMessage args)
    {
        if (_net.IsClient || !TryGetControlledPoint(console, args.Actor, out var point))
            return;
        var coordinates = GetCoordinates(args.Coordinates);
        if (!coordinates.IsValid(EntityManager))
            return;
        var location = _transform.ToMapCoordinates(coordinates);
        EntityUid? nearest = null;
        var distance = 1.5f * 1.5f;
        if (TryComp(point, out SentryTargetingComponent? targeting) && EnsureTrackingFaction(point))
        {
            foreach (var candidate in GetCandidates(point))
            {
                var position = _transform.GetMapCoordinates(candidate);
                var candidateDistance = Vector2.DistanceSquared(position.Position, location.Position);
                if (position.MapId != location.MapId || candidateDistance > distance || !CanTrack(point, candidate, true))
                    continue;
                nearest = candidate;
                distance = candidateDistance;
            }
        }
        SetTracking(point, nearest);
        if (nearest == null)
            _popup.PopupEntity(Loc.GetString("cmu-dropship-track-invalid"), console, args.Actor);
    }

    private void OnConfirmTracking(Entity<DropshipTerminalWeaponsComponent> console,
        ref MohawkChinGunConfirmTrackingMessage args)
    {
        if (_net.IsClient || !TryGetControlledPoint(console, args.Actor, out var point))
            return;
        if (point.Comp.TrackedTarget is not { } target || GetNetEntity(target) != args.Target ||
            !CanTrack(point, target))
        {
            SetTracking(point, null);
            _popup.PopupEntity(Loc.GetString("cmu-dropship-track-invalid"), console, args.Actor);
            return;
        }
        point.Comp.TrackingFireConfirmed = true;
        point.Comp.AuthorizedUntil = _timing.CurTime + point.Comp.AuthorizationDuration;
        console.Comp.TrackingFireConfirmed = true;
        Dirty(point);
        Dirty(console);
    }

    private void OnClearTracking(Entity<DropshipTerminalWeaponsComponent> console,
        ref MohawkChinGunClearTrackingMessage args)
    {
        if (!_net.IsClient && TryGetControlledPoint(console, args.Actor, out var point))
            SetTracking(point, null);
    }

    private void OnAim(Entity<DropshipTerminalWeaponsComponent> console, ref MohawkChinGunAimMessage args)
    {
        if (_net.IsClient || !TryGetControlledPoint(console, args.Actor, out var point) ||
            point.Comp.TrackedTarget != null ||
            point.Comp.PendingManualShot != null && _timing.CurTime >= point.Comp.HeldFireUntil ||
            _timing.CurTime < point.Comp.NextAimUpdate)
            return;
        point.Comp.NextAimUpdate = _timing.CurTime + TimeSpan.FromMilliseconds(75);
        var coordinates = GetCoordinates(args.Coordinates);
        if (coordinates.IsValid(EntityManager) && IsInFlareArea(point, coordinates) &&
            TryGetWeapon(point, out var weapon))
        {
            if (_timing.CurTime < point.Comp.HeldFireUntil)
                point.Comp.HeldAim = coordinates;
            if (!IsAirborne(point))
                RequestAim(point, weapon, coordinates);
        }
    }

    private void OnPan(Entity<DropshipTerminalWeaponsComponent> console, ref MohawkChinGunPanMessage args)
    {
        if (_net.IsClient || !TryGetControlledPoint(console, args.Actor, out var point))
            return;
        if (args.Direction is { } direction && !direction.IsCardinal())
            return;
        SetTracking(point, null);
        var current = _transform.ToMapCoordinates(GetCameraCoordinates(point)).Position;
        point.Comp.CameraPan = Vector2.Zero;
        var center = _transform.ToMapCoordinates(GetCameraCoordinates(point)).Position;
        if (args.Direction is { } pan)
        {
            point.Comp.CameraPan = current + pan.ToIntVec() * 2 - center;
            if (IsAirborne(point) && point.Comp.CameraPan.LengthSquared() > point.Comp.FlareRadius * point.Comp.FlareRadius)
                point.Comp.CameraPan = Vector2.Normalize(point.Comp.CameraPan) * point.Comp.FlareRadius;
            point.Comp.CameraPan = _transform.ToMapCoordinates(GetCameraCoordinates(point)).Position - center;
        }
    }

    private void OnPriority(Entity<DropshipTerminalWeaponsComponent> console, ref MohawkChinGunPriorityMessage args)
    {
        if (_net.IsClient || !console.Comp.Gunnery ||
            !_dropships.TryGetGridDropship(console, out var ship))
            return;
        foreach (var uid in ship.Comp.AttachmentPoints)
        {
            if (!TryComp(uid, out MohawkChinGunComponent? chin) || !IsSeatedOperator(uid, args.Actor) ||
                chin.Operator != null && chin.Operator != args.Actor)
                continue;
            chin.TargetPriority = chin.TargetPriority == MohawkTargetPriority.Distance
                ? MohawkTargetPriority.HighestHealth : MohawkTargetPriority.Distance;
            console.Comp.ChinGunPriority = chin.TargetPriority;
            Dirty(uid, chin);
            Dirty(console);
            return;
        }
    }

    private void OnAcquire(Entity<DropshipTerminalWeaponsComponent> console, ref MohawkChinGunAcquireMessage args)
    {
        if (_net.IsClient || !TryGetControlledPoint(console, args.Actor, out var point))
            return;
        var target = SelectTarget(point);
        SetTracking(point, target);
        if (target == null)
            _popup.PopupEntity(Loc.GetString("cmu-dropship-track-invalid"), console, args.Actor);
    }

    private void OnFlare(Entity<DropshipTerminalWeaponsComponent> console, ref MohawkChinGunFlareMessage args)
    {
        if (_net.IsClient || !TryGetControlledPoint(console, args.Actor, out var point))
            return;
        var flare = GetEntity(args.Flare);
        if (flare != null && !IsUsableFlare(point, flare.Value))
        {
            _popup.PopupEntity(Loc.GetString("cmu-mohawk-gunnery-flare-invalid"), console, args.Actor);
            return;
        }
        SetTracking(point, null);
        console.Comp.Target = flare;
        point.Comp.CameraPan = Vector2.Zero;
        Dirty(console);
    }

    public EntityUid? SelectTarget(Entity<MohawkChinGunComponent> point, float? radius = null)
    {
        if (!TryComp(point, out SentryTargetingComponent? targeting) || !EnsureTrackingFaction(point) ||
            !TryComp(point, out GunshipDirectFirePointComponent? mount))
            return null;
        var rotation = _transform.GetWorldRotation(Transform(point).GridUid ?? point.Owner);
        var origin = _transform.GetMapCoordinates(point);
        var muzzle = origin.Position + rotation.RotateVec(new Vector2(mount.LateralOffset, mount.ForwardOffset));
        EntityUid? selected = null;
        var bestDistance = float.PositiveInfinity;
        var bestHealth = float.NegativeInfinity;
        foreach (var candidate in GetCandidates(point))
        {
            if (!CanTrack(point, candidate, true))
                continue;
            if (IsAirborne(point) && point.Comp.Console is { } console &&
                TryComp(console, out DropshipTerminalWeaponsComponent? terminal) && terminal.Target is { } flare)
                muzzle = _transform.GetMapCoordinates(flare).Position;
            var distance = Vector2.DistanceSquared(muzzle, _transform.GetMapCoordinates(candidate).Position);
            if (radius is { } limit && distance > limit * limit)
                continue;
            var health = 0f;
            if (TryComp(candidate, out DamageableComponent? damage) &&
                _thresholds.TryGetDeadThreshold(candidate, out var maximum))
                health = MathF.Max(0, (maximum.Value - _damage.GetTotalDamage((candidate, damage))).Float());
            if (selected != null && (point.Comp.TargetPriority == MohawkTargetPriority.Distance
                    ? distance >= bestDistance
                    : health < bestHealth || health == bestHealth && distance >= bestDistance))
                continue;
            selected = candidate;
            bestDistance = distance;
            bestHealth = health;
        }
        return selected;
    }

    private IEnumerable<EntityUid> GetCandidates(Entity<MohawkChinGunComponent> point)
    {
        var candidates = new HashSet<EntityUid>();
        if (IsAirborne(point) && point.Comp.Console is { } console &&
            TryComp(console, out DropshipTerminalWeaponsComponent? terminal) &&
            terminal.Target is { } flare && IsUsableFlare(point, flare))
        {
            _nearbyMobs.Clear();
            _lookup.GetEntitiesInRange(_transform.GetMapCoordinates(flare), point.Comp.FlareRadius, _nearbyMobs);
            foreach (var mob in _nearbyMobs)
                candidates.Add(mob.Owner);
        }
        else if (TryComp(point, out SentryTargetingComponent? targeting))
        {
            foreach (var candidate in _targeting.GetNearbyIffHostiles((point.Owner, targeting), GetScanRange(point)))
                candidates.Add(candidate);
        }
        return candidates;
    }

    private void FireAt(Entity<MohawkChinGunComponent> point, EntityUid actor, EntityUid weapon,
        GunComponent gun, EntityCoordinates coordinates, EntityUid? target = null)
    {
        if (IsAirborne(point))
        {
            if (point.Comp.Console is { } console &&
                TryComp(console, out DropshipTerminalWeaponsComponent? terminal))
            {
                if (!_weapons.TryFireChinGunAirburst(point, weapon, actor, coordinates, terminal) &&
                    _timing.CurTime >= point.Comp.NextFireWarning && gun.NextFire <= _timing.CurTime)
                {
                    point.Comp.NextFireWarning = _timing.CurTime + TimeSpan.FromSeconds(1);
                    var key = !IsInFlareArea(point, coordinates)
                        ? "cmu-mohawk-gunnery-flare-invalid"
                        : !_directFire.TryGetFeedAmmo(point, out var ammo) || ammo.Comp.Rounds <= 0
                            ? "cmu-gunship-direct-fire-no-ammunition" : "cmu-mohawk-gunnery-airburst-blocked";
                    _popup.PopupEntity(Loc.GetString(key), console, actor);
                }
            }
            point.Comp.PendingManualShot = null;
        }
        else
            _guns.AttemptShoot(actor, (weapon, gun), coordinates, target);
    }

    private void OnTrigger(Entity<DropshipTerminalWeaponsComponent> console, ref MohawkChinGunTriggerMessage args)
    {
        if (_net.IsClient || !TryGetControlledPoint(console, args.Actor, out var point))
            return;
        if (!args.Held)
        {
            point.Comp.HeldAim = null;
            point.Comp.HeldFireUntil = TimeSpan.Zero;
            point.Comp.PendingManualShot = null;
            return;
        }
        if (args.Coordinates is not { } netCoordinates)
            return;
        var coordinates = GetCoordinates(netCoordinates);
        if (!coordinates.IsValid(EntityManager))
            return;
        if (_timing.CurTime >= point.Comp.HeldFireUntil)
            SetTracking(point, null);
        point.Comp.HeldAim = coordinates;
        // Renewed by the client while the trigger is held; losing focus/input cannot leave a stuck trigger.
        point.Comp.HeldFireUntil = _timing.CurTime + TimeSpan.FromMilliseconds(350);
        if (!IsAirborne(point) && TryGetWeapon(point, out var weapon))
            RequestAim(point, weapon, coordinates);
    }

    private void OnFeed(Entity<DropshipTerminalWeaponsComponent> console, ref MohawkChinGunFeedMessage args)
    {
        if (_net.IsClient || !console.Comp.Gunnery ||
            !_dropships.TryGetGridDropship(console, out var ship))
            return;
        foreach (var uid in ship.Comp.AttachmentPoints)
        {
            if (!TryComp(uid, out MohawkChinGunComponent? chin) || !IsSeatedOperator(uid, args.Actor) ||
                chin.Operator != null && chin.Operator != args.Actor)
                continue;
            if (args.Feed is { } feed)
            {
                if (feed is < 0 or > 1)
                    return;
                chin.ActiveFeed = feed;
                chin.AlternateFeeds = false;
            }
            else
                chin.AlternateFeeds = !chin.AlternateFeeds;
            Dirty(uid, chin);
            SyncFeedDisplay((uid, chin));
            return;
        }
    }

    private void SyncFeedDisplay(Entity<MohawkChinGunComponent> point)
    {
        if (point.Comp.Console is not { } console ||
            !TryComp(console, out DropshipTerminalWeaponsComponent? terminal) ||
            !TryComp(point, out DropshipWeaponPointComponent? mount))
            return;
        string Describe(string? slotId)
        {
            if (slotId != null && _containers.TryGetContainer(point, slotId, out var slot))
            {
                foreach (var uid in slot.ContainedEntities)
                {
                    if (TryComp(uid, out DropshipAmmoComponent? ammo))
                        return Loc.GetString("cmu-mohawk-gunnery-feed-ammo",
                            ("ammo", Name(uid)), ("rounds", ammo.Rounds));
                }
            }
            return Loc.GetString("cmu-mohawk-gunnery-feed-empty");
        }
        var one = Describe(mount.AmmoContainerSlotId);
        var two = Describe(mount.SecondAmmoContainerSlotId);
        if (terminal.ChinGunFeed == point.Comp.ActiveFeed && terminal.ChinGunAlternateFeeds == point.Comp.AlternateFeeds &&
            terminal.ChinGunFeedOne == one && terminal.ChinGunFeedTwo == two)
            return;
        terminal.ChinGunFeed = point.Comp.ActiveFeed;
        terminal.ChinGunAlternateFeeds = point.Comp.AlternateFeeds;
        terminal.ChinGunFeedOne = one;
        terminal.ChinGunFeedTwo = two;
        Dirty(console, terminal);
    }

    public bool IsSafeAirburst(EntityUid point, MapCoordinates target, float radius)
    {
        if (!TryComp(point, out MohawkChinGunComponent? chin) || !EnsureTrackingFaction((point, chin)) ||
            !TryComp(point, out SentryTargetingComponent? targeting))
            return false;
        _nearbyMobs.Clear();
        _lookup.GetEntitiesInRange(target, radius, _nearbyMobs);
        foreach (var mob in _nearbyMobs)
        {
            if (!_targeting.IsValidTarget((point, targeting), mob.Owner))
                return false;
        }
        return true;
    }

    private float GetScanRange(Entity<MohawkChinGunComponent> point)
    {
        var mount = Comp<GunshipDirectFirePointComponent>(point);
        return point.Comp.Range + new Vector2(mount.LateralOffset, mount.ForwardOffset).Length();
    }

    private bool IsUsableFlare(Entity<MohawkChinGunComponent> point, EntityUid flare)
    {
        if (!IsAirborne(point) || TerminatingOrDeleted(flare) || !HasComp<FlareSignalComponent>(flare) ||
            !TryComp(flare, out DropshipTargetComponent? target) || !target.IsTargetableByWeapons ||
            point.Comp.Console is not { } console ||
            !TryComp(console, out DropshipTerminalWeaponsComponent? terminal))
            return false;
        var netFlare = GetNetEntity(flare);
        return terminal.Targets.Exists(candidate => candidate.Id == netFlare);
    }

    public bool IsInFlareArea(Entity<MohawkChinGunComponent> point, EntityCoordinates coordinates)
    {
        if (point.Comp.Console is not { } console ||
            !TryComp(console, out DropshipTerminalWeaponsComponent? terminal) ||
            terminal.Target is not { } flare || !HasComp<FlareSignalComponent>(flare))
            return !IsAirborne(point);
        if (!IsUsableFlare(point, flare))
            return false;
        var target = _transform.ToMapCoordinates(coordinates);
        var anchor = _transform.GetMapCoordinates(flare);
        return target.MapId == anchor.MapId &&
               Vector2.DistanceSquared(target.Position, anchor.Position) <= point.Comp.FlareRadius * point.Comp.FlareRadius;
    }

    private bool EnsureTrackingFaction(Entity<MohawkChinGunComponent> point)
    {
        if (!_dropships.TryGetGridFaction(point, out var faction))
            return false;
        if (string.Equals(point.Comp.AssignedFaction, faction, StringComparison.OrdinalIgnoreCase))
            return true;
        if (!_targeting.TryApplyDefaultFaction(point, faction))
            return false;
        point.Comp.AssignedFaction = faction;
        return true;
    }

    public bool RequestAim(Entity<MohawkChinGunComponent> point, EntityUid weapon, EntityCoordinates coordinates)
    {
        if (!TryComp(weapon, out GunshipDirectFireWeaponComponent? direct) ||
            !_directFire.TryResolveMount((weapon, direct), out var mount, out var grid, out _, out _) ||
            !coordinates.IsValid(EntityManager))
            return false;
        var location = _transform.ToMapCoordinates(coordinates);
        var rotation = _transform.GetWorldRotation(grid);
        var origin = _transform.GetMapCoordinates(point);
        var position = origin.Position + rotation.RotateVec(new Vector2(mount.Comp.LateralOffset, mount.Comp.ForwardOffset));
        var delta = location.Position - position;
        if (origin.MapId != location.MapId || delta.LengthSquared() > point.Comp.Range * point.Comp.Range ||
            Vector2.Dot(rotation.RotateVec(Vector2.UnitY), delta) < 0 ||
            !GunshipDirectFireSystem.TryGetClampedAim(rotation, position, location.Position, direct.GimbalDegrees,
                out _, out var degrees))
            return false;
        point.Comp.DesiredAimDegrees = degrees;
        return MathF.Abs(mount.Comp.AimOffsetDegrees - degrees) < 0.5f;
    }

    public bool IsSafeShot(Entity<MohawkChinGunComponent> point, MapCoordinates muzzle, MapCoordinates target,
        EntityUid weapon)
    {
        if (!EnsureTrackingFaction(point) || !TryComp(point, out SentryTargetingComponent? targeting) ||
            !_interaction.InRangeUnobstructed(muzzle, target, point.Comp.Range, CollisionGroup.Opaque,
                predicate: blocker => blocker == point.Owner || blocker == weapon || HasComp<MobStateComponent>(blocker) ||
                    !TryComp(blocker, out OccluderComponent? occluder) || !occluder.Enabled))
            return false;
        var line = target.Position - muzzle.Position;
        var lengthSquared = line.LengthSquared();
        var friendlyRadius = 2f;
        if (TryComp(weapon, out GunshipDirectFireWeaponComponent? direct) &&
            _directFire.TryResolveMount((weapon, direct), out _, out _, out _, out var ammo) && ammo != null)
            friendlyRadius = ammo.FriendlyImpactRadius;
        _nearbyMobs.Clear();
        _lookup.GetEntitiesInRange(muzzle, point.Comp.Range + friendlyRadius, _nearbyMobs);
        foreach (var mob in _nearbyMobs)
        {
            if (_targeting.IsValidTarget((point.Owner, targeting), mob.Owner))
                continue;
            var location = _transform.GetMapCoordinates(mob);
            if (location.MapId != muzzle.MapId)
                continue;
            var fraction = lengthSquared > 0 ? Math.Clamp(Vector2.Dot(location.Position - muzzle.Position, line) / lengthSquared, 0, 1) : 0;
            if (Vector2.DistanceSquared(location.Position, muzzle.Position + fraction * line) < 0.75f * 0.75f ||
                Vector2.DistanceSquared(location.Position, target.Position) < friendlyRadius * friendlyRadius)
                return false;
        }
        return true;
    }
}
