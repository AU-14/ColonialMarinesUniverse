using System.Numerics;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Sentry;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared.CMU14.Dropship.TacticalLand;
using Content.Shared.Eye;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
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
            _guns.AttemptShoot(args.Actor, (weapon, gun), coordinates);
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
        if (!IsGrounded(point))
        {
            message = Loc.GetString("cmu-mohawk-gunnery-ground-only");
            return false;
        }

        if (actor == weapon)
            return _net.IsServer && point.Comp.Operator == null;

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
            if (!IsGrounded(uid))
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

            if (!TryGetWeapon(uid, out var weapon) ||
                !TryComp(args.Actor, out EyeComponent? eye))
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
            console.Comp.ChinGunEye = chin.Eye;
            Dirty(console);
            Dirty(point);
            return;
        }
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
        return _transform.ToCoordinates(new MapCoordinates(coords.Position +
            rotation.RotateVec(new Vector2(mount.LateralOffset, mount.ForwardOffset + 5f)), coords.MapId));
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
            if (chin.Operator is { } actor)
            {
                if (!IsGrounded(uid) || !IsSeatedOperator(uid, actor) || chin.Console is not { } console ||
                    !_ui.IsUiOpen(console, DropshipTerminalWeaponsUi.Key, actor) || !TryGetWeapon(uid, out _))
                    ReleaseControl(point);
                else if (chin.Eye is { } eye)
                {
                    if (chin.TrackedTarget is { } target)
                    {
                        if (!CanTrack(point, target) ||
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
                            _transform.SetCoordinates(eye, _transform.GetMoverCoordinates(target));
                            if (chin.TrackingFireConfirmed && TryGetWeapon(uid, out var trackedWeapon) &&
                                TryComp(trackedWeapon, out GunComponent? trackedGun))
                                _guns.AttemptShoot(actor, (trackedWeapon, trackedGun), Transform(target).Coordinates, target);
                            continue;
                        }
                    }
                    _transform.SetCoordinates(eye, GetCameraCoordinates(uid));
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

            var rotation = _transform.GetWorldRotation(grid);
            var origin = _transform.GetMapCoordinates(uid);
            var position = origin.Position + rotation.RotateVec(
                new Vector2(mount.Comp.LateralOffset, mount.Comp.ForwardOffset));
            EntityUid? nearest = null;
            var nearestDistance = chin.Range * chin.Range;
            foreach (var candidate in _targeting.GetNearbyIffHostiles((uid, targeting), chin.Range))
            {
                if (TryComp(candidate, out MobStateComponent? mob) && mob.CurrentState == MobState.Dead ||
                    _dropships.TryGetGridDropship(candidate, out var targetShip) &&
                    _dropships.TryGetGridDropship(uid, out var ship) && targetShip.Owner == ship.Owner)
                    continue;
                var target = _transform.GetMapCoordinates(candidate);
                var delta = target.Position - position;
                var distance = delta.LengthSquared();
                if (target.MapId != origin.MapId || distance <= 0.0001f || distance > nearestDistance ||
                    Vector2.Dot(rotation.RotateVec(Vector2.UnitY), delta) < -0.0001f)
                    continue;
                var muzzle = new MapCoordinates(position + Vector2.Normalize(delta) * direct.MuzzleOffset,
                    origin.MapId);
                if (!_interaction.InRangeUnobstructed(muzzle, target, chin.Range,
                        predicate: blocker => blocker == candidate || blocker == uid || blocker == weapon))
                    continue;
                nearest = candidate;
                nearestDistance = distance;
            }

            if (nearest is { } hostile)
                _guns.AttemptShoot(weapon, (weapon, gun), Transform(hostile).Coordinates, hostile);
            else
                _appearance.SetData(uid, GunshipDirectFireVisuals.AimOffsetDegrees, 0f);
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
        if (TerminatingOrDeleted(target) || !IsGrounded(point) ||
            !TryComp(point, out SentryTargetingComponent? targeting) ||
            !EnsureTrackingFaction(point) ||
            !TryGetWeapon(point, out var weapon) ||
            !TryComp(weapon, out GunshipDirectFireWeaponComponent? direct) ||
            !_directFire.TryResolveMount((weapon, direct), out var mount, out var grid, out _, out _) ||
            TryComp(target, out MobStateComponent? mob) && mob.CurrentState == MobState.Dead)
            return false;
        var hostile = knownHostile;
        if (!knownHostile)
        {
            foreach (var candidate in _targeting.GetNearbyIffHostiles((point.Owner, targeting), point.Comp.Range))
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
            Vector2.Dot(rotation.RotateVec(Vector2.UnitY), delta) < -0.0001f)
            return false;
        var muzzle = new MapCoordinates(position + Vector2.Normalize(delta) * direct.MuzzleOffset, origin.MapId);
        return _interaction.InRangeUnobstructed(muzzle, location, point.Comp.Range,
            predicate: blocker => blocker == target || blocker == point.Owner || blocker == weapon);
    }

    private void SetTracking(Entity<MohawkChinGunComponent> point, EntityUid? target)
    {
        point.Comp.TrackedTarget = target;
        point.Comp.TrackingFireConfirmed = false;
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
            foreach (var candidate in _targeting.GetNearbyIffHostiles((point.Owner, targeting), point.Comp.Range))
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
}
