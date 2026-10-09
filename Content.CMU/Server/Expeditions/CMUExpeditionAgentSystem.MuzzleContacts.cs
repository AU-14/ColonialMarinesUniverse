using System.Linq;
using System.Numerics;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    [Dependency] private IRobustRandom _visionRandom = default!;

    // The ghillie system owns the GunComponent subscription. Observe the same directed
    // event through metadata without replacing its handler or requiring a second broadcast.
    private void InitializeVision() => SubscribeLocalEvent<MetaDataComponent, GunShotEvent>(OnObservedMuzzleFlash);

    private void OnObservedMuzzleFlash(Entity<MetaDataComponent> entity, ref GunShotEvent args)
    {
        if (!_npcs.Enabled || !TryComp<GunComponent>(entity, out var gun))
            return;
        Entity<GunComponent> ent = (entity, gun);
        // Use the same suppression hook as the real effect (including attached silencers).
        var flash = new GunMuzzleFlashAttemptEvent();
        RaiseLocalEvent(ent, ref flash);
        HearShot(ent, ref args, flash.Cancelled);
        if (flash.Cancelled || !args.Ammo.Any(ammo => ammo.Shootable is AmmoComponent { MuzzleFlash: not null }))
            return;
        var now = _timing.CurTime;
        var origin = _transform.ToMapCoordinates(args.FromCoordinates);
        var query = EntityQueryEnumerator<CMUExpeditionAgentComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var agent, out var transform))
        {
            if (uid == args.User || HasComp<ActorComponent>(uid) || !_mobs.IsAlive(uid) || transform.MapID != origin.MapId ||
                now < agent.NextFlashObservation || !_transform.InRange(transform.Coordinates, args.FromCoordinates, agent.DetectionRange) ||
                !SightLine(uid, args.FromCoordinates, agent.DetectionRange) || !ExpeditionHostiles(uid, agent).Contains(args.User))
                continue;
            // Freeze the observed flash in map coordinates. Neither a radio report nor a
            // remembered entity reference may supply live position/velocity after this event.
            var error = new Vector2(_visionRandom.NextFloat(-0.8f, 0.8f), _visionRandom.NextFloat(-0.8f, 0.8f));
            agent.FlashPosition = _transform.ToCoordinates(transform.MapUid!.Value, origin.Offset(error));
            agent.FlashShooter = args.User;
            agent.FlashUntil = now + agent.FlashMemory;
            agent.NextFlashObservation = now + TimeSpan.FromSeconds(0.4);
            agent.NextThink = now;
            if (agent.Target is { } target && Visible(uid, target, agent.DetectionRange))
                continue;
            agent.LastSeen = agent.FlashPosition;
            agent.LastContact = now;
            agent.ForgetAt = now + agent.MemoryDuration;
            agent.ContactFromRadio = false;
            agent.LastContactWasMelee = false;
            agent.VisionDecision = "observed-muzzle-flash";
        }
    }

    private bool TryFlashAim(EntityUid uid, CMUExpeditionAgentComponent agent, out EntityCoordinates point)
    {
        point = default;
        if (agent.FlashPosition is not { } flash || _timing.CurTime >= agent.FlashUntil ||
            agent.FlashShooter is not { } shooter || !Exists(shooter) || !AcceptOrderedContact(uid, agent, shooter) ||
            !SightLine(uid, flash, Math.Min(agent.FireRange, WeaponFireRange(uid, agent))))
            return false;
        point = flash;
        return true;
    }
}
