using System.Numerics;
using Content.Shared.CMU14.Fighter;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;

namespace Content.Server.CMU14.Fighter;

public sealed partial class FighterSystem
{
    [Dependency] private FighterManpadSystem _manpads = default!;
    [Dependency] private SharedHandsSystem _manpadHands = default!;
    private static readonly EntProtoId ManpadLaunchVisual = "CMUFighterManpadLaunch";
    private static readonly EntProtoId ManpadLaunchFlash = "CMUFighterManpadFlash";
    private static readonly SoundSpecifier ManpadLockSound = new SoundPathSpecifier("/Audio/CMU14/Fighter/manpad-lock.wav");
    private static readonly SoundSpecifier ManpadLaunchSound = new SoundPathSpecifier("/Audio/CMU14/Fighter/manpad-launch.wav");
    private static readonly SoundSpecifier ManpadIncomingSound = new SoundPathSpecifier("/Audio/CMU14/Fighter/manpad-incoming.wav");

    private void OnManpadShutdown(Entity<FighterManpadComponent> launcher, ref ComponentShutdown args) => CancelManpadWindup(launcher.Comp);
    private void OnManpadAimStopped(Entity<FighterManpadComponent> launcher, ref FighterManpadAimStoppedEvent args) => CancelManpadWindup(launcher.Comp);

    private void CancelManpadWindup(FighterManpadComponent launcher)
    {
        launcher.WindupAudio = _audio.Stop(launcher.WindupAudio);
        launcher.TrackingAudio = _audio.Stop(launcher.TrackingAudio);
        if (launcher.WindupVisual is { } visual && !TerminatingOrDeleted(visual)) QueueDel(visual);
        launcher.WindupVisual = null;
        launcher.Target = null;
        launcher.LaunchAt = default;
    }

    private bool CanAcquireManpad(FighterManpadComponent launcher, EntityUid terrain, Vector2 position,
        Entity<FighterAircraftComponent, FighterAirCombatComponent, FighterWeaponsComponent> target)
        => (launcher.IgnoreIFF || _iff.Hostile(launcher.Faction, target.Comp3.Faction)) &&
           InGroundWeaponSector(terrain, position, target);

    private bool InGroundWeaponSector(EntityUid terrain, Vector2 position,
        Entity<FighterAircraftComponent, FighterAirCombatComponent, FighterWeaponsComponent> target)
    {
        var aircraft = target.Comp1;
        if (aircraft.TerrainMap != terrain || !aircraft.Flying || aircraft.ForcedRetreat ||
            target.Comp2.Incoming || !HasCombatPilot(aircraft)) return false;
        var sector = FighterAirCombat.SectorAt(aircraft.Battlefield, position);
        return sector >= 0 && sector == FighterAirCombat.SectorAt(aircraft.Battlefield, aircraft.Position);
    }

    // Runs with the existing ten-Hz interception update. Acquisition never shortens the flare window.
    private void UpdateManpads()
    {
        var now = _timing.CurTime;
        var updateRadar = now >= _nextManpadRadarUpdate;
        if (updateRadar)
        {
            _nextManpadRadarUpdate = now + TimeSpan.FromSeconds(.25);
            _activeRadarUsers.Clear();
        }
        var query = EntityQueryEnumerator<FighterManpadComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var launcher, out var xform))
        {
            if (updateRadar) UpdateManpadRadar((uid, launcher), xform);
            // Revalidate the operator at acquisition and launch, including stun/death and container changes.
            // Being wielded alone never arms the launcher, and dropping it cannot leave a queued shot.
            if (!_manpads.IsAiming((uid, launcher)))
            {
                _manpads.StopAiming((uid, launcher));
                if (launcher.Target != null) CancelManpadWindup(launcher);
                continue;
            }
            if (now < launcher.ReadyAt || !launcher.IgnoreIFF && string.IsNullOrWhiteSpace(launcher.Faction) ||
                xform.MapUid is not { } terrain)
            {
                if (launcher.Target != null) CancelManpadWindup(launcher);
                continue;
            }
            while (TryComp(terrain, out CMUZLevelMapComponent? level) && level.Depth > 0 && level.MapBelow is { } below)
                terrain = below;
            var position = _transform.GetWorldPosition(uid);
            // The directional in-hand sprite sits on the operator's shoulder. Keep the seeker,
            // ignition and backblast on that tube rather than the character's feet.
            var user = launcher.AimingUser!.Value;
            var facing = _transform.GetWorldRotation(user).GetCardinalDir();
            var tubeDirection = facing.ToVec();
            var leftHand = _manpadHands.TryGetHand(user, _manpadHands.GetActiveHand(user), out var hand) &&
                           hand.Value.Location == HandLocation.Left;
            // EMBLR's north/south sprites draw the tube end above the hands, even when facing
            // south. A forward world-space offset would put its seeker below the operator's feet.
            var muzzleOffset = facing switch
            {
                Direction.South => new Vector2(leftHand ? .16f : -.2f, .35f),
                Direction.North => new Vector2(leftHand ? -.2f : .16f, .35f),
                Direction.East => new Vector2(.48f, .16f),
                _ => new Vector2(-.48f, .16f),
            };
            var muzzle = position + muzzleOffset;
            var retained = false;
            foreach (var target in _combatAircraft)
            {
                if (launcher.Target is { } locked && locked != target.Owner || !CanAcquireManpad(launcher, terrain, position, target)) continue;
                if (launcher.Target == null)
                {
                    var coordinates = new EntityCoordinates(xform.MapUid.Value, muzzle);
                    var visualUid = Spawn(ManpadLaunchVisual, coordinates);
                    var visual = Comp<FighterManpadVisualComponent>(visualUid);
                    var direction = target.Comp1.Position - position;
                    visual.Direction = direction.LengthSquared() > .001f ? Vector2.Normalize(direction) : Vector2.UnitY;
                    visual.TubeDirection = tubeDirection;
                    visual.WindupSeconds = Math.Max(0, (float) launcher.AcquisitionTime.TotalSeconds);
                    visual.StartedAt = now;
                    visual.ExpiresAt = now + launcher.AcquisitionTime + TimeSpan.FromSeconds(FighterManpadVisualComponent.Lifetime);
                    Comp<TimedDespawnComponent>(visualUid).Lifetime += visual.WindupSeconds;
                    Dirty(visualUid, visual);
                    launcher.Target = target.Owner;
                    launcher.LaunchAt = now + launcher.AcquisitionTime;
                    launcher.WindupVisual = visualUid;
                    launcher.WindupAudio = _fighterAudio.PlayGround(ManpadLockSound, Transform(visualUid).Coordinates, 24, -4);
                    launcher.TrackingAudio = _audio.PlayPvs(ManpadLockSound, target.Owner, AudioParams.Default.WithVolume(-12))?.Entity;
                }
                if (launcher.WindupVisual is not { } launchUid || !TryComp(launchUid, out FighterManpadVisualComponent? launch) ||
                    Transform(launchUid).MapUid != xform.MapUid || Vector2.DistanceSquared(_transform.GetWorldPosition(launchUid), muzzle) > .25f ||
                    launch.TubeDirection != tubeDirection)
                    break;
                retained = true;
                if (now < launcher.LaunchAt) break;

                // Use the normal EMBLR reload provider, but the air interception owns the
                // missile. Consuming the cartridge must not spawn a second ground projectile.
                var ammunition = new List<(EntityUid? Entity, IShootable Shootable)>();
                var takeAmmo = new TakeAmmoEvent(1, ammunition, xform.Coordinates, user);
                RaiseLocalEvent(uid, takeAmmo);
                if (ammunition.Count == 0)
                {
                    retained = false;
                    break;
                }
                foreach (var (cartridge, _) in ammunition)
                {
                    if (cartridge is { } round) Del(round);
                }

                launcher.ReadyAt = now + launcher.ReloadTime;
                BeginInterception(target, position, launcher.MissileFlightTime, fromGround: true);
                launch.Launched = true;
                launch.Direction = -target.Comp2.IncomingDirection;
                launch.StartedAt = now;
                launch.ExpiresAt = now + TimeSpan.FromSeconds(FighterManpadVisualComponent.Lifetime);
                Comp<TimedDespawnComponent>(launchUid).Lifetime = FighterManpadVisualComponent.Lifetime;
                Dirty(launchUid, launch);
                // Once fired, moving or removing the launcher cannot recall the missile or exhaust.
                launcher.WindupVisual = null;
                CancelManpadWindup(launcher);
                Spawn(ManpadLaunchFlash, Transform(launchUid).Coordinates);
                _fighterAudio.PlayGround(ManpadLaunchSound, Transform(launchUid).Coordinates, 65, 0);
                _audio.PlayPvs(ManpadLaunchSound, target.Owner, AudioParams.Default.WithVolume(-14));
                var sector = FighterAirCombat.SectorAt(target.Comp1.Battlefield, position);
                Log.Info($"MANPAD launched: {ToPrettyString(uid)} -> {ToPrettyString(target.Owner)}, sector {sector}.");
                break;
            }
            if (!retained && launcher.Target != null) CancelManpadWindup(launcher);
        }
        UpdateBoilerAirDefense(updateRadar);
        if (updateRadar) ClearInactiveManpadRadars();
    }
}
