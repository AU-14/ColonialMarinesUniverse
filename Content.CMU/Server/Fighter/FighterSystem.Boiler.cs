using System.Numerics;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared.CMU14.Fighter;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Spawners;

namespace Content.Server.CMU14.Fighter;

public sealed partial class FighterSystem
{
    [Dependency] private FighterBoilerAirDefenseSystem _boilerAirDefense = default!;
    [Dependency] private XenoPlasmaSystem _boilerPlasma = default!;
    private static readonly SoundSpecifier BoilerPlasmaSound = new SoundPathSpecifier("/Audio/_RMC14/Xeno/blobattack.ogg");
    private static readonly SoundSpecifier BoilerPrepareSound = new SoundCollectionSpecifier("XenoDrool");

    private void CancelBoilerWindup(FighterBoilerAirDefenseComponent boiler)
    {
        boiler.WindupAudio = _audio.Stop(boiler.WindupAudio);
        if (boiler.WindupVisual is { } visual && !TerminatingOrDeleted(visual)) QueueDel(visual);
        boiler.WindupVisual = null;
        boiler.Target = null;
        boiler.LaunchAt = default;
    }

    private void OnBoilerStopped(Entity<FighterBoilerAirDefenseComponent> ent, ref FighterBoilerAirDefenseStoppedEvent args) => CancelBoilerWindup(ent.Comp);

    private void UpdateBoilerAirDefense(bool updateRadar)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FighterBoilerAirDefenseComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var boiler, out var xform))
        {
            if (!boiler.Watching || !_boilerAirDefense.CanWatch(uid) || xform.MapUid is not { } terrain)
            {
                _boilerAirDefense.SetWatching((uid, boiler), false);
                if (boiler.Target != null) CancelBoilerWindup(boiler);
                continue;
            }
            while (TryComp(terrain, out CMUZLevelMapComponent? level) && level.Depth > 0 && level.MapBelow is { } below)
                terrain = below;
            var position = _transform.GetWorldPosition(uid);
            var loaded = TryComp(uid, out XenoPlasmaComponent? plasma) && plasma.Plasma >= boiler.PlasmaCost;
            if (updateRadar)
                SendAirspaceRadar(uid, uid, terrain, position, true, loaded, boiler.ReadyAt, boiler.LaunchAt, true, boiler.Target, organic: true);
            _areas.CanOrbitalBombard(new EntityCoordinates(xform.MapUid.Value, position), out var roofed);
            if (now < boiler.ReadyAt || !loaded || roofed)
            {
                if (boiler.Target != null) CancelBoilerWindup(boiler);
                continue;
            }
            var retained = false;
            foreach (var target in _combatAircraft)
            {
                if (boiler.Target is { } locked && locked != target.Owner || !InGroundWeaponSector(terrain, position, target)) continue;
                if (boiler.Target == null)
                {
                    boiler.Target = target.Owner;
                    boiler.StartedPosition = position;
                    boiler.LaunchAt = now + boiler.AcquisitionTime;
                    var visual = Spawn("CMUFighterBoilerPlasma", xform.Coordinates);
                    boiler.WindupVisual = visual;
                    var effect = Comp<FighterPlasmaVisualComponent>(visual);
                    effect.StartedAt = now;
                    effect.ExpiresAt = boiler.LaunchAt + TimeSpan.FromSeconds(3);
                    Dirty(visual, effect);
                    boiler.WindupAudio = _audio.PlayPvs(BoilerPrepareSound, uid)?.Entity;
                }
                if (Vector2.DistanceSquared(position, boiler.StartedPosition) > .2f * .2f ||
                    boiler.WindupVisual is not { } launch || !TryComp(launch, out FighterPlasmaVisualComponent? bolt) ||
                    Transform(launch).MapUid != xform.MapUid) break;
                retained = true;
                if (now < boiler.LaunchAt) break;
                if (!_boilerPlasma.TryRemovePlasma(uid, boiler.PlasmaCost))
                {
                    retained = false;
                    break;
                }
                boiler.ReadyAt = now + boiler.Cooldown;
                BeginInterception(target, position, TimeSpan.FromSeconds(5), fromGround: true, plasma: true);
                bolt.Launched = true;
                bolt.Direction = -target.Comp2.IncomingDirection;
                bolt.StartedAt = now;
                bolt.ExpiresAt = now + TimeSpan.FromSeconds(3);
                Comp<TimedDespawnComponent>(launch).Lifetime = 3;
                Dirty(launch, bolt);
                boiler.WindupVisual = null;
                CancelBoilerWindup(boiler);
                _fighterAudio.PlayGround(BoilerPlasmaSound, xform.Coordinates, 45, 0);
                break;
            }
            if (!retained && boiler.Target != null) CancelBoilerWindup(boiler);
        }
    }
}
