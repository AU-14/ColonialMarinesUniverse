using Content.Server.CMU14.Hearing;
using Content.Server.Radio.EntitySystems;
using Content.Shared._RMC14.Areas;
using Content.Shared.CMU14.Hearing;
using Content.Shared.CMU14.ShotAlert;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.ShotAlert;

public sealed class CMUShotAlertSystem : EntitySystem
{
    [Dependency] private AreaSystem _areas = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUGunFiredEvent>(OnGunFired);
        SubscribeLocalEvent<CMUExplosionSpawnedEvent>(OnExplosion);
    }

    private void OnGunFired(ref CMUGunFiredEvent args)
    {
        TryAlert(_transform.ToMapCoordinates(args.From), "cmu-shot-alert-gunfire");
    }

    private void OnExplosion(ref CMUExplosionSpawnedEvent args)
    {
        TryAlert(args.Epicenter, "cmu-shot-alert-explosion");
    }

    private void TryAlert(MapCoordinates source, string message)
    {
        if (source.MapId == MapId.Nullspace)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUShotAlertComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var alert, out var xform))
        {
            if (now < alert.NextAlert || !xform.Anchored)
                continue;

            var position = _transform.GetMapCoordinates(uid, xform);
            if (position.MapId != source.MapId ||
                (position.Position - source.Position).LengthSquared() > alert.Range * alert.Range)
            {
                continue;
            }

            alert.NextAlert = now + alert.Cooldown;

            var location = _areas.TryGetArea(source, out _, out var areaProto)
                ? areaProto.Name
                : Loc.GetString("cmu-shot-alert-unknown-location");

            _radio.SendRadioMessage(uid, Loc.GetString(message, ("location", location)), alert.Channel, uid);
        }
    }
}
