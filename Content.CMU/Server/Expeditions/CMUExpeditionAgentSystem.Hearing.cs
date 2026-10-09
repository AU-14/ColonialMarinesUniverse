using System.Numerics;
using Content.Shared.Doors.Components;
using Content.Shared.Doors;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private void InitializeHearing() => SubscribeLocalEvent<DoorComponent, DoorStateChangedEvent>(OnHeardDoor);

    private void OnHeardDoor(Entity<DoorComponent> ent, ref DoorStateChangedEvent args)
    {
        if (args.State == DoorState.Open)
            HearNoise(Transform(ent).Coordinates, 5, "door", null);
    }

    private void HearShot(Entity<GunComponent> gun, ref GunShotEvent args, bool suppressed)
    {
        var sound = gun.Comp.SoundGunshotModified ?? gun.Comp.SoundGunshot;
        if (sound == null || sound.Params.Volume <= -60)
            return;
        HearNoise(args.FromCoordinates, suppressed ? 4 : 18, "gunfire", args.User);
    }

    private void HearNoise(EntityCoordinates source, float range, string kind, EntityUid? shooter)
    {
        if (!_npcs.Enabled)
            return;
        var origin = _transform.ToMapCoordinates(source);
        var nearby = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(origin.MapId, origin.Position, range, nearby);
        foreach (var uid in nearby)
        {
            if (!TryComp<CMUExpeditionAgentComponent>(uid, out var agent) || !_mobs.IsAlive(uid) ||
                HasComp<ActorComponent>(uid) || _timing.CurTime < agent.NextHearing ||
                shooter is { } known && IsFriendly(uid, known))
                continue;
            // Walls shorten hearing range instead of making sound a perfect target sensor.
            var clear = _interaction.InRangeUnobstructed(uid, source, range);
            if (!clear && !_transform.InRange(Transform(uid).Coordinates, source, range * .35f))
                continue;
            var error = clear ? 2f : 4f;
            agent.HeardPoint = _transform.ToCoordinates(Transform(uid).MapUid!.Value,
                origin.Offset(new Vector2(_visionRandom.NextFloat(-error, error), _visionRandom.NextFloat(-error, error))));
            agent.HeardUntil = _timing.CurTime + TimeSpan.FromSeconds(10);
            agent.NextHearing = _timing.CurTime + TimeSpan.FromSeconds(3);
            // No Target, LastSeen or FlashPosition is assigned by hearing.
            Decision(agent, "heard-noise", kind);
        }
    }

    private bool InvestigateSound(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (agent.HeardPoint is not { } heard || now >= agent.HeardUntil || agent.Target != null ||
            agent.OrderedDestination != null || agent.TravelGoal != null || agent.Entrench || agent.Action != null ||
            agent.Treatment != null || agent.SupplySource != null || agent.RecoveryUntil > now ||
            agent.Duty is CMUSquadDuty.Medic or CMUSquadDuty.RearGuard ||
            agent.Home is not { } home || !_transform.InRange(home, heard, agent.LeashRange))
            return false;
        if (_transform.InRange(Transform(uid).Coordinates, heard, 3))
        {
            agent.HeardPoint = null;
            _steering.Unregister(uid);
            agent.State = CMUExpeditionAgentState.Watch;
            return false;
        }
        Decision(agent, "investigate-sound", "uncertain-location-no-shot-permission");
        InvestigateContact(uid, agent, heard, now);
        return true;
    }
}
