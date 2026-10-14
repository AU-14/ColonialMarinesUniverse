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
using Robust.Shared.Player;
using Content.Shared._RMC14.Armor;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Inventory;

namespace Content.Shared.CMU14.Dropship.DirectFire;

public sealed partial class MohawkChinGunSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
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
                subs.Event<MohawkChinGunFreeFireMessage>(OnFreeFire);
                subs.Event<MohawkChinGunArMessage>(OnAr);
                subs.Event<MohawkChinGunIffMessage>(OnIff);
                subs.Event<MohawkChinGunApproveAiMessage>(OnApproveAi);
                subs.Event<MohawkChinGunVisionMessage>(OnVision);
            });
        SubscribeLocalEvent<MohawkChinGunComponent, ComponentShutdown>(OnShutdown);
        InitializeDamage();
        SubscribeNetworkEvent<MohawkChinGunInputEvent>(OnKeyboardInput);
        SubscribeLocalEvent<EntityTerminatingEvent>(OnGunneryEntityTerminating);
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
        if (point.Comp.Disabled || !point.Comp.Calibrated || !point.Comp.IffConfigured ||
            !EntityManager.System<DropshipFireControlBootSystem>().IsWeaponReady(weapon))
        {
            message = Loc.GetString(point.Comp.Disabled ? "cmu-mohawk-gunnery-disabled" :
                !point.Comp.Calibrated ? "cmu-mohawk-setup-screw" :
                !point.Comp.IffConfigured ? "cmu-mohawk-setup-iff" : "cmu-dropship-hardpoint-unready");
            return false;
        }
        if (!IsGrounded(point) && !IsAirborne(point))
        {
            message = Loc.GetString("cmu-mohawk-gunnery-ground-only");
            return false;
        }

        if (actor == weapon)
            return _net.IsServer && point.Comp.Operator == null && point.Comp.DefensiveFire &&
                   (IsGrounded(point) || IsFreeFireActive(point.Comp) && IsAirborne(point));

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
            if (chin.Disabled)
                error = "cmu-mohawk-gunnery-disabled";
            else if (!chin.Calibrated)
                error = "cmu-mohawk-setup-screw";
            else if (!chin.IffConfigured)
                error = "cmu-mohawk-setup-iff";
            else if (!IsGrounded(uid) && !IsAirborne(uid))
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
            if (!EntityManager.System<DropshipFireControlBootSystem>().IsWeaponReady(weapon))
            {
                _popup.PopupEntity(Loc.GetString("cmu-dropship-hardpoint-unready"), console, args.Actor);
                return;
            }

            if (TryComp(weapon, out DropshipWeaponComponent? dropshipWeapon) &&
                dropshipWeapon.Skills != null && !_skills.HasSkills(args.Actor, dropshipWeapon.Skills))
            {
                _popup.PopupEntity(Loc.GetString("rmc-laser-designator-not-skilled"), console, args.Actor);
                return;
            }

            chin.Operator = args.Actor;
            chin.ApprovedAutomaticTarget = null;
            chin.AutomaticTarget = null;
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
            chin.PreviousDrawLight = eye.DrawLight;
            chin.ControlledWeapon = weapon;
            chin.Eye = Spawn(null, GetCameraCoordinates(uid));
            EnsureComp<EyeComponent>(chin.Eye.Value);
            ApplyVision(point, chin.Eye.Value, chin.AirborneControl);
            _eyes.SetTarget(args.Actor, chin.Eye, eye);
            ApplyVision(point, args.Actor, chin.AirborneControl, eye);
            chin.AddedRemoteOperator = !HasComp<RemoteWeaponOperatorComponent>(args.Actor);
            var remote = EnsureComp<RemoteWeaponOperatorComponent>(args.Actor);
            chin.PreviousPlatform = remote.Platform;
            remote.Platform = Transform(uid).GridUid;
            remote.SelectedWeapon = weapon;
            Dirty(args.Actor, remote);
            console.Comp.ChinGunOperator = args.Actor;
            console.Comp.ScreenOne.Weapon = GetNetEntity(weapon);
            console.Comp.ChinGunPriority = chin.TargetPriority;
            _weapons.SetChinGunCamera(console, chin.Eye);
            Dirty(console);
            Dirty(point);
            _adminLog.Add(LogType.RMCDropshipWeapon,
                $"{ToPrettyString(args.Actor)} took manual control of M90 chin gun {ToPrettyString(uid)} ({(chin.AirborneControl ? "airborne" : "grounded")}).");
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
        if (TryGetWeapon(point, out var weapon) && TryComp(weapon, out GunshipDirectFireWeaponComponent? direct) &&
            GunshipDirectFireSystem.TryGetClampedAim(rotation, muzzle, muzzle + delta, direct.GimbalDegrees, out var direction, out _))
            delta = direction * delta.Length();
        if (delta.LengthSquared() > chin.Range * chin.Range)
            delta = Vector2.Normalize(delta) * chin.Range;
        if (!IsAirborne(point) && chin.VisionMode != MohawkChinGunVisionMode.Thermal &&
            !CameraLineClear(point, muzzle, muzzle + delta, coords.MapId))
        {
            var low = 0f;
            var high = 1f;
            for (var i = 0; i < 8; i++)
            {
                var middle = (low + high) / 2;
                if (CameraLineClear(point, muzzle, muzzle + delta * middle, coords.MapId))
                    low = middle;
                else
                    high = middle;
            }
            delta *= MathF.Max(0, low - 0.02f);
        }
        return _transform.ToCoordinates(new MapCoordinates(muzzle + delta, coords.MapId));
    }

    private bool CameraLineClear(EntityUid point, Vector2 origin, Vector2 target, MapId map)
    {
        TryGetWeapon(point, out var weapon);
        return _interaction.InRangeUnobstructed(new MapCoordinates(origin, map), new MapCoordinates(target, map),
            Comp<MohawkChinGunComponent>(point).Range, CollisionGroup.Opaque,
            predicate: blocker => blocker == point || blocker == weapon || HasComp<MobStateComponent>(blocker) ||
                !TryComp(blocker, out OccluderComponent? occluder) || !occluder.Enabled);
    }

    private void OnShutdown(Entity<MohawkChinGunComponent> point, ref ComponentShutdown args)
    {
        if (_net.IsServer)
        {
            ReleaseControl(point);
            if (point.Comp.AutomaticEye is { } eye)
                QueueDel(eye);
        }
    }

    private void OnGunneryEntityTerminating(ref EntityTerminatingEvent args)
    {
        if (_net.IsClient)
            return;
        var removed = args.Entity.Owner;
        var points = EntityQueryEnumerator<MohawkChinGunComponent>();
        while (points.MoveNext(out var uid, out var chin))
        {
            if (TerminatingOrDeleted(uid))
                continue;
            var point = new Entity<MohawkChinGunComponent>(uid, chin);
            if (chin.Operator == removed || chin.Eye == removed)
                ReleaseControl(point);
            if (chin.TrackedTarget == removed)
                SetTracking(point, null);
            if (chin.AutomaticTarget == removed)
            {
                chin.AutomaticTarget = null;
                chin.ApprovedAutomaticTarget = null;
                chin.NextScan = TimeSpan.Zero;
                Dirty(point);
            }
            if (chin.AutomaticEye == removed)
            {
                chin.AutomaticEye = null;
                chin.NextScan = TimeSpan.Zero;
            }
        }
        var consoles = EntityQueryEnumerator<DropshipTerminalWeaponsComponent>();
        while (consoles.MoveNext(out var uid, out var terminal))
        {
            if (TerminatingOrDeleted(uid))
                continue;
            if (terminal.ChinGunEye == removed)
                _weapons.SetChinGunCamera((uid, terminal), null);
            if (terminal.GunnerFlare == removed || terminal.PilotFlare == removed)
            {
                if (terminal.GunnerFlare == removed)
                    terminal.GunnerFlare = null;
                if (terminal.PilotFlare == removed)
                    terminal.PilotFlare = null;
                Dirty(uid, terminal);
            }
            if (terminal.ChinGunAiTarget != removed)
                continue;
            terminal.ChinGunAiTarget = null;
            terminal.ChinGunAiApproved = false;
            Dirty(uid, terminal);
        }
    }

    private void ReleaseControl(Entity<MohawkChinGunComponent> point)
    {
        SetTracking(point, null);
        if (point.Comp.Operator is { } released)
            _adminLog.Add(LogType.RMCDropshipWeapon,
                $"{ToPrettyString(released)} released manual control of M90 chin gun {ToPrettyString(point)}.");
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
                _eyes.SetDrawLight((actor, eye), point.Comp.PreviousDrawLight);
            }
        }

        if (point.Comp.Console is { } console && !TerminatingOrDeleted(console) &&
            TryComp(console, out DropshipTerminalWeaponsComponent? terminal))
        {
            terminal.ChinGunOperator = null;
            _weapons.SetChinGunCamera((console, terminal), null);
            terminal.ChinGunAirborne = false;
            terminal.ChinGunAr = false;
            terminal.Target = null;
            Dirty(console, terminal);
            _weapons.ShareFlare(console, null, true);
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
        point.Comp.ArMode = false;
        point.Comp.KeyboardPan = Vector2.Zero;
        point.Comp.KeyboardPanUntil = TimeSpan.Zero;
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
            SyncFreeFireDisplay(point);
            if (chin.Disabled)
                continue;
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
                    if (_timing.CurTime >= chin.KeyboardPanUntil)
                        chin.KeyboardPan = Vector2.Zero;
                    if (_timing.CurTime < chin.KeyboardPanUntil && chin.KeyboardPan != Vector2.Zero)
                    {
                        var current = _transform.ToMapCoordinates(GetCameraCoordinates(uid)).Position;
                        chin.CameraPan = Vector2.Zero;
                        var center = _transform.ToMapCoordinates(GetCameraCoordinates(uid)).Position;
                        chin.CameraPan = current - center;
                        chin.CameraPan += chin.KeyboardPan * (6f * frameTime);
                        if (IsAirborne(uid) && chin.CameraPan.LengthSquared() > chin.FlareRadius * chin.FlareRadius)
                            chin.CameraPan = Vector2.Normalize(chin.CameraPan) * chin.FlareRadius;
                        chin.CameraPan = _transform.ToMapCoordinates(GetCameraCoordinates(uid)).Position - center;
                        if (!IsAirborne(uid) && TryGetWeapon(uid, out var panWeapon))
                            RequestAim(point, panWeapon, GetCameraCoordinates(uid));
                    }
                    if (chin.TrackedTarget is { } target)
                    {
                        if (!IsMarkable(point, target) ||
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

            var freeFire = IsFreeFireActive(chin);
            if ((!IsGrounded(uid) && !(freeFire && IsAirborne(uid))) ||
                !TryGetWeapon(uid, out var weapon) || !TryComp(weapon, out GunComponent? gun) ||
                !TryComp(weapon, out GunshipDirectFireWeaponComponent? direct))
                continue;

            if (!chin.Calibrated || !chin.IffConfigured ||
                !EntityManager.System<DropshipFireControlBootSystem>().IsWeaponReady(weapon))
                continue;

            var selected = chin.AutomaticTarget;
            if (_timing.CurTime >= chin.NextScan || selected is { } retained && !IsAutomaticTarget(point, retained))
            {
                chin.NextScan = _timing.CurTime + TimeSpan.FromMilliseconds(freeFire ? 100 : 250);
                if (IsAirborne(uid))
                    selected = PlanAutomaticFlyby(point);
                else
                {
                    if (!ChooseAutomaticFeed(point, null))
                        continue;
                    selected = SelectAutomaticTarget(point, freeFire ? null : chin.CloseDefenseRange);
                    if (selected is { } groundTarget)
                        ChooseAutomaticFeed(point, groundTarget);
                }
            }
            else if (selected == null)
                continue;
            var observed = selected ?? SelectTarget(point);
            if (chin.ApprovedAutomaticTarget != selected)
                chin.ApprovedAutomaticTarget = null;
            SyncAutomaticCamera(point, selected ?? observed);
            if (observed is { } hostile)
            {
                if (IsGrounded(uid))
                    RequestAim(point, weapon, Transform(hostile).Coordinates);
                if (selected != null && chin.ApprovedAutomaticTarget == selected)
                {
                    chin.DefensiveFire = true;
                    try
                    {
                        if (IsAirborne(uid) && chin.FreeFireConsole is { } freeConsole &&
                            TryComp(freeConsole, out DropshipTerminalWeaponsComponent? terminal))
                            _weapons.TryFireChinGunAirburst(point, weapon, weapon, Transform(hostile).Coordinates, terminal);
                        else
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

    public bool IsFreeFireActive(MohawkChinGunComponent chin)
    {
        return chin.FreeFireEnabled;
    }

    public bool IsAmmoUsable(EntityUid point, DropshipAmmoComponent ammo)
    {
        return IsGrounded(point) ? ammo.GroundCompatible : IsAirborne(point) && ammo.AirborneCompatible;
    }

    public float FiringArc(Entity<MohawkChinGunComponent> point, EntityUid weapon, EntityCoordinates target)
    {
        var closeDefense = point.Comp.Operator == null && IsGrounded(point) &&
                           target.TryDistance(EntityManager, _transform, GetMountCoordinates(point), out var distance) &&
                           distance <= point.Comp.CloseDefenseRange;
        return closeDefense ? 360f : Comp<GunshipDirectFireWeaponComponent>(weapon).GimbalDegrees;
    }

    public EntityCoordinates GetMountCoordinates(Entity<MohawkChinGunComponent> point)
    {
        var mount = Comp<GunshipDirectFirePointComponent>(point);
        var rotation = _transform.GetWorldRotation(Transform(point).GridUid ?? point.Owner);
        var origin = _transform.GetMapCoordinates(point);
        return _transform.ToCoordinates(new MapCoordinates(origin.Position +
            rotation.RotateVec(new Vector2(mount.LateralOffset, mount.ForwardOffset)), origin.MapId));
    }

    public bool IsWithinArc(Entity<MohawkChinGunComponent> point, EntityUid weapon, EntityCoordinates coordinates)
    {
        var rotation = _transform.GetWorldRotation(Transform(point).GridUid ?? point.Owner);
        var origin = _transform.ToMapCoordinates(GetMountCoordinates(point));
        var target = _transform.ToMapCoordinates(coordinates);
        var delta = (-rotation).RotateVec(target.Position - origin.Position);
        var degrees = MathF.Atan2(-delta.X, delta.Y) * 180f / MathF.PI;
        return origin.MapId == target.MapId && delta.LengthSquared() > 0.0001f &&
               MathF.Abs(degrees) <= FiringArc(point, weapon, coordinates) / 2f + 0.001f;
    }

    public string AmmoRestriction(DropshipAmmoComponent ammo)
    {
        return Loc.GetString(!ammo.AirborneCompatible
            ? "cmu-mohawk-gunnery-ammo-ground-only" : "cmu-mohawk-gunnery-ammo-air-only");
    }

    public EntityUid? SelectAutomaticTarget(Entity<MohawkChinGunComponent> point, float? radius = null)
    {
        if (point.Comp.AutomaticTarget is { } current && IsAutomaticTarget(point, current) &&
            (radius == null || DistanceFromMount(point, current) <= radius.Value * radius.Value))
            return current;
        point.Comp.AutomaticTarget = SelectTarget(point, radius);
        return point.Comp.AutomaticTarget;
    }

    private bool IsAutomaticTarget(Entity<MohawkChinGunComponent> point, EntityUid target)
    {
        return !TerminatingOrDeleted(target) && TryComp(target, out MobStateComponent? mob) &&
               mob.CurrentState == MobState.Alive &&
               TryComp(point, out SentryTargetingComponent? targeting) &&
               EnsureTrackingFaction(point) && _targeting.IsValidTarget((point.Owner, targeting), target) &&
               CanTrack(point, target);
    }

    private float DistanceFromMount(Entity<MohawkChinGunComponent> point, EntityUid target)
    {
        var mount = Comp<GunshipDirectFirePointComponent>(point);
        var rotation = _transform.GetWorldRotation(Transform(point).GridUid ?? point.Owner);
        var origin = _transform.GetMapCoordinates(point).Position +
                     rotation.RotateVec(new Vector2(mount.LateralOffset, mount.ForwardOffset));
        return Vector2.DistanceSquared(origin, _transform.GetMapCoordinates(target).Position);
    }

    private void OnFreeFire(Entity<DropshipTerminalWeaponsComponent> console, ref MohawkChinGunFreeFireMessage args)
    {
        if (_net.IsClient || !console.Comp.Gunnery || !_dropships.TryGetGridDropship(console, out var ship))
            return;
        foreach (var uid in ship.Comp.AttachmentPoints)
        {
            if (!TryComp(uid, out MohawkChinGunComponent? chin) || !IsSeatedOperator(uid, args.Actor) ||
                !_ui.IsUiOpen(console.Owner, DropshipTerminalWeaponsUi.Key, args.Actor))
                continue;
            var point = new Entity<MohawkChinGunComponent>(uid, chin);
            if (chin.Disabled)
            {
                if (IsFreeFireActive(chin))
                {
                    EndFreeFire(point);
                    SyncFreeFireDisplay(point);
                    return;
                }
                _popup.PopupEntity(Loc.GetString("cmu-mohawk-gunnery-disabled"), console, args.Actor);
                return;
            }
            if (chin.Operator is { } other && other != args.Actor)
            {
                _popup.PopupEntity(Loc.GetString("cmu-mohawk-gunnery-busy"), console, args.Actor);
                return;
            }
            if (IsFreeFireActive(chin))
            {
                EndFreeFire(point);
                SyncFreeFireDisplay(point);
                return;
            }
            if (!chin.Calibrated || !chin.IffConfigured)
            {
                _popup.PopupEntity(Loc.GetString(!chin.Calibrated ? "cmu-mohawk-setup-screw" : "cmu-mohawk-setup-iff"), console, args.Actor);
                return;
            }
            if (_timing.CurTime < chin.FreeFireReadyAt)
            {
                _popup.PopupEntity(Loc.GetString("cmu-mohawk-free-fire-cooldown",
                    ("seconds", (int) Math.Ceiling((chin.FreeFireReadyAt - _timing.CurTime).TotalSeconds))), console, args.Actor);
                return;
            }
            if (!IsGrounded(uid) && !IsAirborne(uid))
            {
                _popup.PopupEntity(Loc.GetString("cmu-mohawk-gunnery-ground-only"), console, args.Actor);
                return;
            }
            if (!TryGetWeapon(uid, out var weapon) || !TryComp(weapon, out DropshipWeaponComponent? loaded) ||
                loaded.Skills != null && !_skills.HasSkills(args.Actor, loaded.Skills))
            {
                _popup.PopupEntity(Loc.GetString("rmc-laser-designator-not-skilled"), console, args.Actor);
                return;
            }
            var flare = console.Comp.Target;
            if (IsAirborne(uid) && !console.Comp.Targets.Exists(candidate =>
                    GetEntity(candidate.Id) is { } anchor && IsUsableFlare((uid, chin), anchor, console.Comp)))
            {
                _popup.PopupEntity(Loc.GetString("cmu-mohawk-gunnery-flare-invalid"), console, args.Actor);
                return;
            }
            ReleaseControl(point);
            chin.AutomaticTarget = null;
            console.Comp.Target = IsAirborne(uid) ? flare : null;
            _weapons.ShareFlare(console, console.Comp.Target, true);
            chin.FreeFireConsole = console;
            chin.FreeFireEnabled = true;
            chin.ApprovedAutomaticTarget = null;
            chin.NextScan = TimeSpan.Zero;
            Dirty(point);
            Dirty(console);
            SyncFreeFireDisplay(point);
            _adminLog.Add(LogType.RMCDropshipWeapon,
                $"{ToPrettyString(args.Actor)} enabled AI free fire on M90 chin gun {ToPrettyString(uid)}.");
            return;
        }
    }

    private void EndFreeFire(Entity<MohawkChinGunComponent> point)
    {
        point.Comp.FreeFireReadyAt = _timing.CurTime + TimeSpan.FromMinutes(5);
        point.Comp.FreeFireEnabled = false;
        point.Comp.ApprovedAutomaticTarget = null;
        point.Comp.AutomaticTarget = null;
        if (point.Comp.FreeFireConsole is { } console &&
            TryComp(console, out DropshipTerminalWeaponsComponent? terminal))
        {
            if (point.Comp.Operator == null)
            {
                terminal.Target = null;
                _weapons.ShareFlare(console, null, true);
            }
            terminal.ChinGunAiTarget = null;
            terminal.ChinGunAiApproved = false;
            Dirty(console, terminal);
        }
        point.Comp.FreeFireConsole = null;
        Dirty(point);
        _adminLog.Add(LogType.RMCDropshipWeapon, $"AI free fire on M90 chin gun {ToPrettyString(point)} was disabled.");
    }

    private void SyncFreeFireDisplay(Entity<MohawkChinGunComponent> point)
    {
        if (!_dropships.TryGetGridDropship(point, out var ship))
            return;
        var cooldown = point.Comp.FreeFireEnabled ? 0 :
            Math.Max(0, (int) Math.Ceiling((point.Comp.FreeFireReadyAt - _timing.CurTime).TotalSeconds));
        var health = TryComp(point, out DamageableComponent? damage)
            ? Math.Max(0, (int) Math.Ceiling(point.Comp.MaxHealth - _damage.GetTotalDamage((point, damage)).Float()))
            : (int) point.Comp.MaxHealth;
        var consoles = EntityQueryEnumerator<DropshipTerminalWeaponsComponent>();
        while (consoles.MoveNext(out var uid, out var terminal))
        {
            if (!terminal.Gunnery || !_dropships.TryGetGridDropship(uid, out var consoleShip) ||
                consoleShip.Owner != ship.Owner ||
                terminal.ChinGunFreeFireCooldown == cooldown &&
                terminal.ChinGunDisabled == point.Comp.Disabled && terminal.ChinGunHealth == health &&
                terminal.ChinGunIff == point.Comp.IffEnabled &&
                terminal.ChinGunSetupReady == (point.Comp.Calibrated && point.Comp.IffConfigured) &&
                terminal.ChinGunAiEnabled == point.Comp.FreeFireEnabled &&
                terminal.ChinGunAiTarget == point.Comp.AutomaticTarget &&
                terminal.ChinGunAiApproved == (point.Comp.ApprovedAutomaticTarget != null) &&
                terminal.ChinGunVision == point.Comp.VisionMode)
                continue;
            terminal.ChinGunFreeFireCooldown = cooldown;
            terminal.ChinGunDisabled = point.Comp.Disabled;
            terminal.ChinGunHealth = health;
            terminal.ChinGunIff = point.Comp.IffEnabled;
            terminal.ChinGunSetupReady = point.Comp.Calibrated && point.Comp.IffConfigured;
            terminal.ChinGunAiEnabled = point.Comp.FreeFireEnabled;
            terminal.ChinGunAiTarget = point.Comp.AutomaticTarget;
            terminal.ChinGunAiApproved = point.Comp.ApprovedAutomaticTarget != null;
            terminal.ChinGunVision = point.Comp.VisionMode;
            Dirty(uid, terminal);
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
        if (!IsMarkable(point, target) || (!IsGrounded(point) && !IsAirborne(point)) ||
            !TryGetWeapon(point, out var weapon) ||
            !TryComp(weapon, out GunshipDirectFireWeaponComponent? direct) ||
            !_directFire.TryResolveMount((weapon, direct), out var mount, out var grid, out _, out _))
            return false;
        if (IsAirborne(point))
            return IsInFlareArea(point, Transform(target).Coordinates) &&
                   _weapons.CanChinGunImpactAt(Transform(target).Coordinates,
                       _directFire.TryGetFeedAmmo(point, out var airborneAmmo) ? airborneAmmo.Comp.ZLevelPenetration : 0);
        var rotation = _transform.GetWorldRotation(grid);
        var origin = _transform.GetMapCoordinates(point);
        var position = origin.Position + rotation.RotateVec(new Vector2(mount.Comp.LateralOffset, mount.Comp.ForwardOffset));
        var location = _transform.GetMapCoordinates(target);
        var delta = location.Position - position;
        if (location.MapId != origin.MapId || delta.LengthSquared() < 0.0001f ||
            delta.LengthSquared() > point.Comp.Range * point.Comp.Range ||
            !IsWithinArc(point, weapon, Transform(target).Coordinates) ||
            !IsInFlareArea(point, Transform(target).Coordinates))
            return false;
        var muzzle = new MapCoordinates(position + Vector2.Normalize(delta) * direct.MuzzleOffset, origin.MapId);
        return _interaction.InRangeUnobstructed(muzzle, location, point.Comp.Range, CollisionGroup.Opaque,
            predicate: blocker => blocker == target || blocker == point.Owner || blocker == weapon ||
                !TryComp(blocker, out OccluderComponent? occluder) || !occluder.Enabled);
    }

    public bool IsMarkable(Entity<MohawkChinGunComponent> point, EntityUid target)
    {
        return !TerminatingOrDeleted(target) && _directFire.TryGetFeedAmmo(point, out var ammo) &&
               (!ammo.Comp.LivingTargetsOnly || TryComp(target, out MobStateComponent? mob) && mob.CurrentState != MobState.Dead);
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
            var candidates = _lookup.GetEntitiesInRange(location, 1.5f, LookupFlags.Uncontained);
            foreach (var candidate in candidates)
            {
                var position = _transform.GetMapCoordinates(candidate);
                var candidateDistance = Vector2.DistanceSquared(position.Position, location.Position);
                if (position.MapId != location.MapId || candidateDistance > distance || !CanTrack(point, candidate))
                    continue;
                nearest = candidate;
                distance = candidateDistance;
            }
        }
        SetTracking(point, nearest);
        if (nearest == null)
            _popup.PopupEntity(Loc.GetString("cmu-mohawk-gunnery-mark-invalid"), console, args.Actor);
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

    private void OnAr(Entity<DropshipTerminalWeaponsComponent> console, ref MohawkChinGunArMessage args)
    {
        if (_net.IsClient || !TryGetControlledPoint(console, args.Actor, out var point))
            return;
        point.Comp.ArMode = !point.Comp.ArMode;
        point.Comp.HeldAim = null;
        point.Comp.HeldFireUntil = TimeSpan.Zero;
        console.Comp.ChinGunAr = point.Comp.ArMode;
        Dirty(point);
        Dirty(console);
    }

    private bool TryGetSeatedPoint(Entity<DropshipTerminalWeaponsComponent> console, EntityUid actor,
        out Entity<MohawkChinGunComponent> point)
    {
        point = default;
        if (!console.Comp.Gunnery || !_ui.IsUiOpen(console.Owner, DropshipTerminalWeaponsUi.Key, actor) ||
            !_dropships.TryGetGridDropship(console, out var ship))
            return false;
        foreach (var uid in ship.Comp.AttachmentPoints)
            if (TryComp(uid, out MohawkChinGunComponent? chin) && IsSeatedOperator(uid, actor))
            {
                point = (uid, chin);
                return true;
            }
        return false;
    }

    private void OnIff(Entity<DropshipTerminalWeaponsComponent> console, ref MohawkChinGunIffMessage args)
    {
        if (_net.IsClient)
            return;
        if (!TryGetSeatedPoint(console, args.Actor, out var point))
        {
            _popup.PopupEntity(Loc.GetString("cmu-mohawk-gunnery-seat"), console, args.Actor);
            return;
        }
        if (!point.Comp.IffConfigured)
        {
            _popup.PopupEntity(Loc.GetString("cmu-mohawk-setup-iff"), console, args.Actor);
            return;
        }
        point.Comp.IffEnabled = !point.Comp.IffEnabled;
        point.Comp.ApprovedAutomaticTarget = null;
        point.Comp.AutomaticTarget = null;
        point.Comp.NextScan = TimeSpan.Zero;
        SetTracking(point, null);
        Dirty(point);
        SyncFreeFireDisplay(point);
        _popup.PopupEntity(Loc.GetString(point.Comp.IffEnabled ? "cmu-mohawk-iff-on" : "cmu-mohawk-iff-off"),
            console, args.Actor);
        _adminLog.Add(LogType.RMCDropshipWeapon,
            $"{ToPrettyString(args.Actor)} {(point.Comp.IffEnabled ? "enabled" : "disabled")} IFF protection on M90 chin gun {ToPrettyString(point)}.");
    }

    private void OnVision(Entity<DropshipTerminalWeaponsComponent> console, ref MohawkChinGunVisionMessage args)
    {
        if (_net.IsClient)
            return;
        if (!TryGetSeatedPoint(console, args.Actor, out var point))
        {
            _popup.PopupEntity(Loc.GetString("cmu-mohawk-gunnery-seat"), console, args.Actor);
            return;
        }
        point.Comp.VisionMode = point.Comp.VisionMode switch
        {
            MohawkChinGunVisionMode.Normal => MohawkChinGunVisionMode.Night,
            MohawkChinGunVisionMode.Night => MohawkChinGunVisionMode.Thermal,
            _ => MohawkChinGunVisionMode.Normal,
        };
        Dirty(point);
        if (point.Comp.Eye is { } eye && !TerminatingOrDeleted(eye))
            ApplyVision(point, eye, point.Comp.AirborneControl);
        if (point.Comp.Operator is { } actor && TryComp(actor, out EyeComponent? actorEye) &&
            actorEye.Target == point.Comp.Eye)
            ApplyVision(point, actor, point.Comp.AirborneControl, actorEye);
        if (point.Comp.AutomaticEye is { } automatic && !TerminatingOrDeleted(automatic))
            ApplyVision(point, automatic, IsAirborne(point));
        SyncFreeFireDisplay(point);
        _popup.PopupEntity(Loc.GetString(VisionLoc(point.Comp.VisionMode)), console, args.Actor);
        _adminLog.Add(LogType.RMCDropshipWeapon,
            $"{ToPrettyString(args.Actor)} switched M90 chin gun {ToPrettyString(point)} optics to {point.Comp.VisionMode}.");
    }

    public static string VisionLoc(MohawkChinGunVisionMode mode)
    {
        return mode switch
        {
            MohawkChinGunVisionMode.Night => "cmu-mohawk-vision-night",
            MohawkChinGunVisionMode.Thermal => "cmu-mohawk-vision-thermal",
            _ => "cmu-mohawk-vision-normal",
        };
    }

    /// <summary>
    /// Night optics render fullbright; thermal is fullbright and ignores walls for viewing even groundside.
    /// Shots are still blocked by walls.
    /// </summary>
    private void ApplyVision(Entity<MohawkChinGunComponent> point, EntityUid eye, bool airborne, EyeComponent? comp = null)
    {
        if (!Resolve(eye, ref comp, false))
            return;
        _eyes.SetDrawFov(eye, !airborne && point.Comp.VisionMode != MohawkChinGunVisionMode.Thermal, comp);
        _eyes.SetDrawLight((eye, comp), point.Comp.VisionMode == MohawkChinGunVisionMode.Normal);
    }

    private void OnApproveAi(Entity<DropshipTerminalWeaponsComponent> console, ref MohawkChinGunApproveAiMessage args)
    {
        if (_net.IsClient)
            return;
        if (!TryGetSeatedPoint(console, args.Actor, out var point))
        {
            _popup.PopupEntity(Loc.GetString("cmu-mohawk-gunnery-seat"), console, args.Actor);
            return;
        }
        var target = point.Comp.AutomaticTarget;
        if (point.Comp.Operator != null || target == null || GetNetEntity(target.Value) != args.Target ||
            !IsAutomaticTarget(point, target.Value))
        {
            _popup.PopupEntity(Loc.GetString("cmu-mohawk-ai-approval-invalid"), console, args.Actor);
            return;
        }
        point.Comp.ApprovedAutomaticTarget = point.Comp.ApprovedAutomaticTarget == target ? null : target;
        Dirty(point);
        SyncAutomaticCamera(point, target);
        _adminLog.Add(LogType.RMCDropshipWeapon,
            $"{ToPrettyString(args.Actor)} {(point.Comp.ApprovedAutomaticTarget != null ? "approved" : "revoked")} M90 AI engagement of {ToPrettyString(target)}.");
    }

    private void SyncAutomaticCamera(Entity<MohawkChinGunComponent> point, EntityUid? target)
    {
        if (!_dropships.TryGetGridDropship(point, out var ship))
            return;
        if (point.Comp.AutomaticEye is not { } camera || TerminatingOrDeleted(camera))
        {
            camera = Spawn(null, GetCameraCoordinates(point));
            EnsureComp<EyeComponent>(camera);
            point.Comp.AutomaticEye = camera;
        }
        _eyes.SetDrawFov(camera, !IsAirborne(point) && point.Comp.VisionMode != MohawkChinGunVisionMode.Thermal);
        _eyes.SetDrawLight(camera, point.Comp.VisionMode == MohawkChinGunVisionMode.Normal);
        _transform.SetCoordinates(camera, target is { } tracked && CanTrack(point, tracked)
            ? Transform(tracked).Coordinates : GetCameraCoordinates(point));
        var consoles = EntityQueryEnumerator<DropshipTerminalWeaponsComponent>();
        while (consoles.MoveNext(out var uid, out var terminal))
        {
            if (!terminal.Gunnery || terminal.ChinGunOperator != null ||
                !_dropships.TryGetGridDropship(uid, out var consoleShip) || consoleShip.Owner != ship.Owner)
                continue;
            _weapons.SetChinGunCamera((uid, terminal), camera);
            if (terminal.ChinGunAiTarget == point.Comp.AutomaticTarget &&
                terminal.ChinGunAiApproved == (point.Comp.ApprovedAutomaticTarget != null) &&
                terminal.ChinGunAirborne == IsAirborne(point))
                continue;
            terminal.ChinGunAiTarget = point.Comp.AutomaticTarget;
            terminal.ChinGunAiApproved = point.Comp.ApprovedAutomaticTarget != null;
            terminal.ChinGunAirborne = IsAirborne(point);
            Dirty(uid, terminal);
        }
    }

    private void OnKeyboardInput(MohawkChinGunInputEvent args, EntitySessionEventArgs session)
    {
        if (_net.IsClient || session.SenderSession.AttachedEntity is not { } actor ||
            !TryGetEntity(args.Console, out var uid) || !TryComp(uid, out DropshipTerminalWeaponsComponent? terminal) ||
            !TryGetControlledPoint((uid.Value, terminal), actor, out var point) ||
            !float.IsFinite(args.Movement.X) || !float.IsFinite(args.Movement.Y))
            return;
        if (args.ToggleAr)
        {
            var toggle = new MohawkChinGunArMessage { Actor = actor };
            OnAr((uid.Value, terminal), ref toggle);
            return;
        }
        if (args.Movement != Vector2.Zero && point.Comp.KeyboardPan == Vector2.Zero)
            SetTracking(point, null);
        point.Comp.KeyboardPan = args.Movement.LengthSquared() > 1 ? Vector2.Normalize(args.Movement) : args.Movement;
        point.Comp.KeyboardPanUntil = _timing.CurTime + TimeSpan.FromMilliseconds(350);
        if (!point.Comp.ArMode)
            return;
        if (args.Aim is not { } aim)
        {
            point.Comp.HeldAim = null;
            point.Comp.HeldFireUntil = TimeSpan.Zero;
            return;
        }
        var trigger = new MohawkChinGunTriggerMessage(args.Held, aim) { Actor = actor };
        OnTrigger((uid.Value, terminal), ref trigger);
        var aiming = new MohawkChinGunAimMessage(aim) { Actor = actor };
        OnAim((uid.Value, terminal), ref aiming);
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
        SetFlareLock(point, console, flare);
        point.Comp.CameraPan = Vector2.Zero;
    }

    private void SetFlareLock(Entity<MohawkChinGunComponent> point,
        Entity<DropshipTerminalWeaponsComponent> console, EntityUid? flare)
    {
        var changed = console.Comp.Target != flare;
        if (!changed)
            return;
        console.Comp.Target = flare;
        Dirty(console);
        _weapons.ShareFlare(console, flare, true);
        _adminLog.Add(LogType.RMCDropshipWeapon,
            $"{ToPrettyString(point.Comp.Operator)} {(flare == null ? "cleared the M90 flare lock" : $"locked M90 chin gun onto flare {ToPrettyString(flare)}")} from {ToPrettyString(console)}.");
        if (flare is { } selected)
        {
            var warning = new MohawkChinGunFlareLockedEvent(selected, point.Comp.FlareRadius);
            RaiseLocalEvent(ref warning);
        }
    }

        private float RemainingHealth(EntityUid target)
        {
            return TryComp(target, out DamageableComponent? damage) &&
                   _thresholds.TryGetDeadThreshold(target, out var maximum)
                ? MathF.Max(0, (maximum.Value - _damage.GetTotalDamage((target, damage))).Float()) : 0f;
        }

        private bool ChooseAutomaticFeed(Entity<MohawkChinGunComponent> point, EntityUid? target)
        {
            if (!TryChooseAutomaticFeed(point, target, out var chosen, out var alternate))
                return false;
            SetAutomaticFeed(point, chosen, alternate);
            return true;
        }

        private void SetAutomaticFeed(Entity<MohawkChinGunComponent> point, int chosen, bool alternate)
        {
            if (point.Comp.ActiveFeed == chosen && point.Comp.AlternateFeeds == alternate)
                return;
            point.Comp.ActiveFeed = chosen;
            point.Comp.AlternateFeeds = alternate;
            Dirty(point);
        }

        private bool TryChooseAutomaticFeed(Entity<MohawkChinGunComponent> point, EntityUid? target,
            out int chosen, out bool alternate, bool requireSafety = true)
        {
            chosen = -1;
            alternate = false;
            Entity<DropshipAmmoComponent>? selectedAmmo = null;
            var best = float.NegativeInfinity;
            var center = target is { } entity ? _transform.GetMapCoordinates(entity) : MapCoordinates.Nullspace;
            var health = target is { } healthTarget ? RemainingHealth(healthTarget) : 0;
            var armor = 0;
            var hostiles = 1;
            if (target is { } armored)
            {
                var protection = new CMGetArmorEvent(SlotFlags.OUTERCLOTHING | SlotFlags.INNERCLOTHING);
                RaiseLocalEvent(armored, ref protection);
                armor = Math.Max(protection.Bullet, protection.XenoArmor);
                _nearbyMobs.Clear();
                _lookup.GetEntitiesInRange(center, 3f, _nearbyMobs);
                hostiles = 0;
                if (TryComp(point, out SentryTargetingComponent? targeting))
                    foreach (var mob in _nearbyMobs)
                        if (mob.Comp.CurrentState == MobState.Alive &&
                            _targeting.IsValidTarget((point.Owner, targeting), mob.Owner))
                            hostiles++;
            }
            for (var feed = 0; feed < 2; feed++)
            {
                if (_directFire.GetFeedAmmo(point, feed) is not { } ammo || ammo.Comp.Rounds <= 0 ||
                    !IsAmmoUsable(point, ammo.Comp))
                    continue;
                if (target is { } impact && IsAirborne(point) &&
                    (!_weapons.CanChinGunImpactAt(Transform(impact).Coordinates, ammo.Comp.ZLevelPenetration) ||
                     requireSafety && !IsSafeAirburst(point, center, ammo.Comp.FriendlyImpactRadius)))
                    continue;
                var payload = ammo.Comp;
                var damage = payload.Damage?.GetTotal().Float() ?? 0;
                var rate = payload.DirectFireRate ?? 3f;
                var penetration = Math.Max(0, armor - payload.ArmorPiercing);
                var effective = damage * (1f - Math.Clamp(penetration / 100f, 0f, 0.9f));
                var shots = health > 0 ? MathF.Max(1, MathF.Ceiling(health / MathF.Max(1, effective))) : 1;
                var score = rate / shots;
                if (hostiles > 1 && payload.Explosion != null)
                    score *= hostiles * (1 + SharedDropshipWeaponSystem.EstimateBlastRadius(payload.Explosion));
                // Favor precise non-explosive suppression against soft isolated targets.
                if (hostiles <= 1 && armor <= 30 && health <= 150 && payload.Explosion == null)
                    score *= 2;
                if (payload.Fire != null && hostiles > 1)
                    score *= 1.5f;
                if (chosen < 0 || score > best || score == best && feed == point.Comp.ActiveFeed)
                {
                    chosen = feed;
                    best = score;
                    selectedAmmo = ammo;
                }
            }
            if (selectedAmmo is not { } selected)
                return false;
            var other = _directFire.GetFeedAmmo(point, 1 - chosen);
            alternate = other is { } second && second.Comp.Rounds > 0 &&
                        IsAmmoUsable(point, second.Comp) &&
                        second.Comp.DirectFireProjectile == selected.Comp.DirectFireProjectile;
            return true;
        }

        private EntityUid? PlanAutomaticFlyby(Entity<MohawkChinGunComponent> point)
        {
            if (point.Comp.FreeFireConsole is not { } console ||
                !TryComp(console, out DropshipTerminalWeaponsComponent? terminal) || !EnsureTrackingFaction(point) ||
                !TryComp(point, out SentryTargetingComponent? targeting))
                return null;
            var oldFeed = point.Comp.ActiveFeed;
            var oldAlternate = point.Comp.AlternateFeeds;
            var bestTarget = (EntityUid?) null;
            var bestFlare = (EntityUid?) null;
            var bestFeed = oldFeed;
            var bestAlternate = oldAlternate;
            var bestDistance = float.PositiveInfinity;
            var bestHealth = float.NegativeInfinity;
            var retained = false;
            foreach (var designation in terminal.Targets)
            {
                if (!TryGetEntity(designation.Id, out var flareUid) || flareUid is not { } flare ||
                    !IsUsableFlare(point, flare))
                    continue;
                var center = _transform.GetMapCoordinates(flare);
                _nearbyMobs.Clear();
                _lookup.GetEntitiesInRange(center, point.Comp.FlareRadius, _nearbyMobs);
                var candidates = new List<Entity<MobStateComponent>>(_nearbyMobs);
                foreach (var candidate in candidates)
                {
                    if (candidate.Comp.CurrentState != MobState.Alive ||
                        !_targeting.IsValidTarget((point.Owner, targeting), candidate.Owner))
                        continue;
                    var keep = point.Comp.AutomaticTarget == candidate.Owner;
                    if (!TryChooseAutomaticFeed(point, candidate.Owner, out var feed, out var alternate) &&
                        (!keep || !TryChooseAutomaticFeed(point, candidate.Owner, out feed, out alternate, false)))
                        continue;
                    var position = _transform.GetMapCoordinates(candidate);
                    var distance = Vector2.DistanceSquared(position.Position, center.Position);
                    if (position.MapId != center.MapId || distance > point.Comp.FlareRadius * point.Comp.FlareRadius)
                        continue;
                    var health = RemainingHealth(candidate);
                    if (bestTarget != null && (retained && !keep || retained == keep &&
                        (point.Comp.TargetPriority == MohawkTargetPriority.Distance ? distance >= bestDistance :
                            health < bestHealth || health == bestHealth && distance >= bestDistance)))
                        continue;
                    bestTarget = candidate.Owner;
                    bestFlare = flare;
                    bestFeed = feed;
                    bestAlternate = alternate;
                    bestDistance = distance;
                    bestHealth = health;
                    retained = keep;
                }
            }
            SetAutomaticFeed(point, bestFeed, bestAlternate);
            point.Comp.AutomaticTarget = bestTarget;
            SetFlareLock(point, (console, terminal), bestFlare);
            return bestTarget;
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
            if (!IsAutomaticTarget(point, candidate))
                continue;
            if (IsAirborne(point) && (point.Comp.Console ?? point.Comp.FreeFireConsole) is { } console &&
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
        if (IsAirborne(point) && (point.Comp.Console ?? point.Comp.FreeFireConsole) is { } console &&
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
                            ? "cmu-gunship-direct-fire-no-ammunition"
                            : !ammo.Comp.AirborneCompatible
                                ? "cmu-mohawk-gunnery-ammo-ground-only" : "cmu-mohawk-gunnery-airburst-blocked";
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
                var previous = chin.ActiveFeed;
                chin.ActiveFeed = feed;
                if (_directFire.TryGetFeedAmmo(uid, out var selectedAmmo) && !IsAmmoUsable(uid, selectedAmmo.Comp))
                {
                    chin.ActiveFeed = previous;
                    _popup.PopupEntity(AmmoRestriction(selectedAmmo.Comp), console, args.Actor);
                    return;
                }
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
        if ((point.Comp.Console ?? point.Comp.FreeFireConsole) is not { } console ||
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
        if (!chin.IffEnabled)
            return true;
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

    private bool IsUsableFlare(Entity<MohawkChinGunComponent> point, EntityUid flare,
        DropshipTerminalWeaponsComponent? terminal = null)
    {
        if (!IsAirborne(point) || TerminatingOrDeleted(flare) || !HasComp<FlareSignalComponent>(flare) ||
            !TryComp(flare, out DropshipTargetComponent? target) || !target.IsTargetableByWeapons ||
            terminal == null && ((point.Comp.Console ?? point.Comp.FreeFireConsole) is not { } console ||
                !TryComp(console, out terminal)))
            return false;
        var netFlare = GetNetEntity(flare);
        return terminal.Targets.Exists(candidate => candidate.Id == netFlare);
    }

    public bool IsInFlareArea(Entity<MohawkChinGunComponent> point, EntityCoordinates coordinates)
    {
        if ((point.Comp.Console ?? point.Comp.FreeFireConsole) is not { } console ||
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
        return point.Comp.IffConfigured;
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
        if (origin.MapId != location.MapId ||
            !IsWithinArc(point, weapon, coordinates) ||
            !GunshipDirectFireSystem.TryGetClampedAim(rotation, position, location.Position, FiringArc(point, weapon, coordinates),
                out _, out var degrees))
            return false;
        point.Comp.DesiredAimDegrees = degrees;
        return MathF.Abs(mount.Comp.AimOffsetDegrees - degrees) < 0.5f;
    }

    public bool IsSafeShot(Entity<MohawkChinGunComponent> point, MapCoordinates muzzle, MapCoordinates target,
        EntityUid weapon)
    {
        var freeTravel = point.Comp.Operator != null && IsGrounded(point) &&
            Vector2.DistanceSquared(_transform.ToMapCoordinates(GetMountCoordinates(point)).Position,
                target.Position) > point.Comp.Range * point.Comp.Range;
        if (!EnsureTrackingFaction(point) || !TryComp(point, out SentryTargetingComponent? targeting) ||
            !freeTravel && !_interaction.InRangeUnobstructed(muzzle, target, MathF.Max(point.Comp.Range, Vector2.Distance(muzzle.Position, target.Position)), CollisionGroup.Opaque,
                predicate: blocker => blocker == point.Owner || blocker == weapon || HasComp<MobStateComponent>(blocker) ||
                    !TryComp(blocker, out OccluderComponent? occluder) || !occluder.Enabled))
            return false;
        if (!point.Comp.IffEnabled)
            return true;
        var line = target.Position - muzzle.Position;
        var lengthSquared = line.LengthSquared();
        var friendlyRadius = 2f;
        if (TryComp(weapon, out GunshipDirectFireWeaponComponent? direct) &&
            _directFire.TryResolveMount((weapon, direct), out _, out _, out _, out var ammo) && ammo != null)
            friendlyRadius = ammo.FriendlyImpactRadius;
        _nearbyMobs.Clear();
        _lookup.GetEntitiesInRange(muzzle, MathF.Max(point.Comp.Range, line.Length()) + friendlyRadius, _nearbyMobs);
        foreach (var mob in _nearbyMobs)
        {
            if (mob.Comp.CurrentState == MobState.Dead)
                continue;
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
