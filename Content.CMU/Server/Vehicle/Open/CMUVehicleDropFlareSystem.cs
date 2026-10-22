using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.CrashLand;
using Content.Shared._RMC14.SupplyDrop;
using Content.Shared.CMU14.Fighter;
using Content.Shared.CMU14.Vehicle.Open;
using Content.Shared.Light.Components;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Vehicle.Open;

/// <summary>
/// Calls a vehicle drop flare's crate down once the lit flare has come to rest on
/// ground open to the sky.
/// </summary>
public sealed partial class CMUVehicleDropFlareSystem : EntitySystem
{
    [Dependency] private AreaSystem _areas = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSupplyDropSystem _supplyDrop = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleDropImpactComponent, CrashLandedEvent>(OnLanded);
    }

    private void OnLanded(Entity<CMUVehicleDropImpactComponent> ent, ref CrashLandedEvent args)
    {
        ent.Comp.ImpactAt = _timing.CurTime;
        Dirty(ent);
        _audio.PlayPvs(ent.Comp.Sound, ent);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUVehicleDropFlareComponent, ExpendableLightComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var flare, out var light, out var xform))
        {
            if (flare.Called)
                continue;

            if (!light.Activated ||
                xform.GridUid == null ||
                HasComp<ThrownItemComponent>(uid) ||
                _containers.IsEntityInContainer(uid))
            {
                flare.SettledAt = null;
                continue;
            }

            if (flare.SettledAt == null || !xform.Coordinates.TryDistance(EntityManager, flare.SettledWhere, out var moved) || moved > 0.05f)
            {
                flare.SettledAt = now;
                flare.SettledWhere = xform.Coordinates;
                continue;
            }

            if (now - flare.SettledAt < flare.Settle)
                continue;

            var target = _transform.GetMapCoordinates(uid, xform);
            if (!_areas.CanSupplyDrop(target))
            {
                if (!flare.Warned)
                    _popup.PopupEntity(Loc.GetString("cmu-vehicle-drop-flare-covered"), uid, PopupType.SmallCaution);

                flare.Warned = true;
                continue;
            }

            flare.Called = true;
            var crate = Spawn(flare.Crate, target);
            // Reuse the supply-drop landing sequence and its marker cleanup.
            var drop = CompOrNull<CanBeSupplyDroppedComponent>(crate);
            var fall = (float) (drop?.DropDuration ?? TimeSpan.FromSeconds(3)).TotalSeconds;
            _supplyDrop.LaunchSupplyDrop(crate,
                target,
                flare.SkyFall,
                fall,
                TimeSpan.FromSeconds(flare.SkyFall + fall + 1f),
                landingEffect: flare.LandingEffect,
                arrivingSound: drop?.ArrivingSound,
                useParachute: flare.UseParachute);

            // The supply drop owns the marker and removes it on landing, including aborted drops.
            var marker = Comp<BeingSupplyDroppedComponent>(crate).LandingEffect;
            if (TryComp(marker, out FighterLaserComponent? laser))
            {
                laser.StartedAt = now;
                laser.Incoming = true;
                laser.IncomingAt = now;
                laser.ExpiresAt = now + TimeSpan.FromSeconds(flare.SkyFall + fall + 1);
                EnsureComp<TimedDespawnComponent>(marker.Value).Lifetime = flare.SkyFall + fall + 1;
                Dirty(marker.Value, laser);
                _audio.PlayPvs(laser.IncomingSound, marker.Value);
            }

            _popup.PopupEntity(Loc.GetString(flare.CalledMessage), uid, PopupType.Medium);
        }
    }
}
