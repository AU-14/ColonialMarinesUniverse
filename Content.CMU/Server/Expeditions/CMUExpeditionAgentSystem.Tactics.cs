using System.Numerics;
using System.Linq;
using Content.Shared.CMU14.Hearing;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private void InitializeTactics()
    {
        SubscribeLocalEvent<CMUGunFiredEvent>(OnNearbyGunfire);
    }

    private void OnNearbyGunfire(ref CMUGunFiredEvent args)
    {
        if (!_npcs.Enabled || !TryComp<GunComponent>(args.Gun, out var gun) ||
            gun.ShootCoordinates is not { } aim)
            return;
        var from = _transform.ToMapCoordinates(args.From);
        var to = _transform.ToMapCoordinates(aim);
        var direction = to.Position - from.Position;
        if (from.MapId != to.MapId || direction.LengthSquared() < 0.01f)
            return;
        var length = direction.Length();
        direction /= length;

        var query = EntityQueryEnumerator<CMUExpeditionAgentComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var agent, out var transform))
        {
            if (HasComp<ActorComponent>(uid) || !_mobs.IsAlive(uid) || transform.MapID != from.MapId ||
                !ExpeditionHostiles(uid, agent).Contains(args.User) || !Visible(uid, args.User, agent.DetectionRange))
                continue;
            var offset = _transform.GetWorldPosition(transform) - from.Position;
            var along = Vector2.Dot(offset, direction);
            if (along < 0 || along > length + 1 || (offset - along * direction).LengthSquared() > 1.5f * 1.5f)
                continue;
            // React to nearby hostile fire only when its source is perceived. Hearing does not grant target tracking.
            agent.Stress = Math.Min(1, agent.Stress + 0.22f);
            agent.SuppressedUntil = _timing.CurTime + TimeSpan.FromSeconds(1.8 - agent.Courage);
            agent.NextThink = _timing.CurTime;
        }
    }

    private bool CanLeaveCover(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        var members = 1;
        var attacking = 0;
        var query = EntityQueryEnumerator<CMUExpeditionAgentComponent>();
        while (query.MoveNext(out var other, out var buddy))
        {
            if (other == uid || HasComp<ActorComponent>(other) || !_mobs.IsAlive(other) ||
                buddy.State is CMUExpeditionAgentState.Disabled or CMUExpeditionAgentState.OutOfAmmo ||
                buddy.Squad != agent.Squad ||
                buddy.Target != agent.Target || !IsFriendly(uid, other) ||
                !_transform.InRange(Transform(uid).Coordinates, Transform(other).Coordinates, 12))
                continue;
            members++;
            if (buddy.State is CMUExpeditionAgentState.Peeking or CMUExpeditionAgentState.Aim or CMUExpeditionAgentState.Engage or CMUExpeditionAgentState.HoldAngle)
                attacking++;
        }
        // Local attack slots stagger exposure; they are released immediately on retreat, injury, death or possession.
        return attacking < (members + 1) / 2;
    }

    private void UpdateEmotions(EntityUid uid, CMUExpeditionAgentComponent agent, float damage, TimeSpan now)
    {
        var elapsed = Math.Clamp((float) (now - agent.LastEmotionUpdate).TotalSeconds, 0, 1);
        agent.LastEmotionUpdate = now;
        agent.Stress = Math.Max(0, agent.Stress - elapsed * 0.08f);
        agent.SupportingAllies = 0;
        agent.HasCoveringAlly = false;
        var query = EntityQueryEnumerator<CMUExpeditionAgentComponent>();
        while (query.MoveNext(out var other, out var buddy))
        {
            if (other == uid || !_mobs.IsAlive(other) || HasComp<ActorComponent>(other) ||
                buddy.Squad != agent.Squad || buddy.State is CMUExpeditionAgentState.Disabled or CMUExpeditionAgentState.OutOfAmmo ||
                buddy.Target != agent.Target || !IsFriendly(uid, other) ||
                !_transform.InRange(Transform(uid).Coordinates, Transform(other).Coordinates, 12))
                continue;
            agent.SupportingAllies++;
            agent.HasCoveringAlly |= buddy.State is CMUExpeditionAgentState.Aim or CMUExpeditionAgentState.Engage or CMUExpeditionAgentState.HoldAngle;
        }
        agent.Initiative = Math.Clamp(agent.Aggression + Math.Min(2, agent.SupportingAllies) * 0.1f -
            agent.Stress * 0.55f - damage / 250, 0.05f, 0.95f);
        agent.Emotion = damage >= agent.EmergencyHealDamage ? CMUExpeditionEmotion.Desperate :
            agent.Stress >= 0.55f + agent.Courage * 0.25f || agent.VisibleThreats.Count > agent.SupportingAllies + 2 ? CMUExpeditionEmotion.Shaken :
            agent.Target == null ? CMUExpeditionEmotion.Calm :
            agent.Initiative >= 0.65f ? CMUExpeditionEmotion.Confident : CMUExpeditionEmotion.Alert;
    }

    private static TimeSpan RecoveryDelay(CMUExpeditionAgentComponent agent) =>
        TimeSpan.FromSeconds(agent.BurstPause.TotalSeconds * (1.2 - agent.Initiative * 0.4 + agent.Stress * 0.6));

    private static int VolleySize(CMUExpeditionAgentComponent agent) =>
        Math.Max(Math.Min(2, agent.BurstSize), agent.BurstSize - (agent.Emotion == CMUExpeditionEmotion.Shaken ? 1 : 0));
}
