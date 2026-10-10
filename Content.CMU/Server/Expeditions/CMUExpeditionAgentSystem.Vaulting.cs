using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Barricade.Components;
using Content.Shared._RMC14.Movement;
using Content.Shared.Climbing.Components;
using Content.Shared.Climbing.Events;
using Content.Shared.Climbing.Systems;
using Content.Shared.DoAfter;
using Content.Shared.NPC.Components;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Systems;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    [Dependency] private ClimbSystem _climb = default!;
    [Dependency] private RMCMovementSystem _vaultMovement = default!;
    private const CollisionGroup VaultMask = CollisionGroup.TableLayer | CollisionGroup.LowImpassable | CollisionGroup.BarricadeImpassable;

    private void InitializeVaulting() =>
        SubscribeLocalEvent<CMUExpeditionAgentComponent, SelfBeforeClimbEvent>(OnBeforeAgentVault);

    private void OnBeforeAgentVault(EntityUid uid, CMUExpeditionAgentComponent agent, SelfBeforeClimbEvent args)
    {
        // Recheck at native completion too: wire or a shutter can change during the do-after.
        if (agent.VaultTarget is { } target && args.BeingClimbedOn.Owner == target
            && (!CanNavigateVault(uid, target) || !_vaultMovement.CanClimbOver(uid, uid, target, popup: false)))
            args.Cancel();
    }

    private CollisionGroup NavigationMask(EntityUid uid, bool planning) =>
        !planning && TryComp<ClimbingComponent>(uid, out var climbing) && climbing.IsClimbing
            ? MovementMask & ~VaultMask : MovementMask;

    private bool CanNavigateVault(EntityUid uid, EntityUid obstacle) =>
        TryComp<ClimbableComponent>(obstacle, out var surface) && surface.Vaultable
        && TryComp<ClimbingComponent>(uid, out var climbing) && climbing.CanClimb
        && !(TryComp<BarbedComponent>(obstacle, out var barbed) && barbed.IsBarbed)
        && !(TryComp<CMUExpeditionAgentComponent>(uid, out var agent)
            && agent.FailedVault == obstacle && _timing.CurTime < agent.AvoidVaultUntil);

    private void CancelVault(CMUExpeditionAgentComponent agent)
    {
        if (agent.VaultDoAfter is { } id && _doAfter.GetStatus(id) == DoAfterStatus.Running)
            _doAfter.Cancel(id);
        agent.VaultDoAfter = null;
        agent.VaultTarget = null;
    }

    private bool MaintainVault(EntityUid uid, CMUExpeditionAgentComponent agent, bool hit, TimeSpan now)
    {
        if (agent.VaultTarget is not { } target)
            return false;
        // The native controller owns the short transition and collision-mask restoration.
        // Do not run another movement, wield or utility action during its do-after.
        if (TryComp<ClimbingComponent>(uid, out var climbing) && climbing.NextTransition != null)
        {
            PauseTravelClock(agent, now);
            _steering.Unregister(uid);
            return true;
        }
        var status = _doAfter.GetStatus(agent.VaultDoAfter);
        if (status == DoAfterStatus.Finished)
        {
            if (climbing?.IsClimbing != true && (!Exists(target)
                || !_transform.InRange(Transform(uid).Coordinates, Transform(target).Coordinates, 0.4f)))
            {
                RejectVault(uid, agent, target);
                return false;
            }
            CancelVault(agent);
            PauseTravelClock(agent, now);
            _bodyClearCache.Clear();
            agent.VaultDecision = "vault-complete";
            return false;
        }
        if (status != DoAfterStatus.Running || hit || now >= agent.VaultUntil
            || !CanNavigateVault(uid, target) || GrenadeDanger(Transform(uid).Coordinates))
        {
            RejectVault(uid, agent, target);
            return false;
        }
        PauseTravelClock(agent, now);
        _steering.Unregister(uid);
        return true;
    }

    /// <summary>Execute the first climb on a planned leg using the same action as a player.</summary>
    private bool PrepareVaultPassage(EntityUid uid, CMUExpeditionAgentComponent agent, ref EntityCoordinates destination)
    {
        var start = Transform(uid).Coordinates;
        if (TraversablePassage(uid, start, destination))
            return true;
        var from = _transform.ToMapCoordinates(start);
        var to = _transform.ToMapCoordinates(destination);
        var delta = to.Position - from.Position;
        if (from.MapId != to.MapId || delta.LengthSquared() < 0.01f)
            return true;
        var center = (from.Position + to.Position) / 2;
        var bounds = new Box2Rotated(Box2.CenteredAround(center,
            new Vector2(delta.Length() + AgentBodyRadius * 2, AgentBodyRadius * 2)), delta.ToAngle(), center);
        var fixtures = new HashSet<FixtureProxy>();
        _lookup.GetFixturesIntersecting(from.MapId, bounds, fixtures, new FixtureQueryArgs(new QueryFilter
        {
            MaskBits = (long) NavigationMask(uid, false),
        }, IgnoreShapeSkin: true));
        var first = fixtures.Where(f => f.Fixture.Hard && f.Body.CanCollide && f.Entity != uid
                && !HasComp<NpcFactionMemberComponent>(f.Entity))
            .OrderBy(f => Vector2.DistanceSquared(from.Position, _transform.GetWorldPosition(f.Entity)))
            .Select(f => (EntityUid?) f.Entity).FirstOrDefault();
        // Non-climbable folding barricades and wired obstacles need a route around them.
        if (first is not { } obstacle || !CanNavigateVault(uid, obstacle))
            return true;
        var surface = Comp<ClimbableComponent>(obstacle);
        var landing = Transform(obstacle).Coordinates;
        if (!_transform.InRange(start, landing, Math.Min(surface.Range, 1.25f)))
        {
            // Follow only the physically clear prefix, so steering never pushes into the rim.
            var approach = start;
            var localEnd = _transform.ToCoordinates(start.EntityId, to);
            var steps = Math.Min(40, (int) MathF.Ceiling(delta.Length() / 0.15f));
            for (var i = 1; i <= steps; i++)
            {
                var point = new EntityCoordinates(start.EntityId, Vector2.Lerp(start.Position, localEnd.Position, i / (float) steps));
                if (!TraversablePassage(uid, start, point))
                    break;
                approach = point;
            }
            if (_transform.InRange(start, approach, 0.05f))
            {
                RejectVault(uid, agent, obstacle);
                return false;
            }
            destination = approach;
            agent.VaultDecision = "approaching-vault";
            return true;
        }
        // ClimbSystem moves to the surface's origin. Validate that whole sweep, not just
        // the route ray, so an offset platform cannot pull the body through a nearby wall.
        if (!TraversablePassage(uid, start, landing, vault: obstacle)
            || Reserved(uid, landing) || GrenadeDanger(landing)
            || !_climb.CanVault(surface, uid, obstacle, out _)
            || TryComp<ClimbingComponent>(uid, out var current) && current.IsClimbing)
        {
            RejectVault(uid, agent, obstacle);
            return false;
        }
        _steering.Unregister(uid);
        _physics.SetLinearVelocity(uid, Vector2.Zero);
        if (!_climb.TryClimb(uid, uid, obstacle, out var id) || id == null)
        {
            RejectVault(uid, agent, obstacle);
            return false;
        }
        agent.VaultTarget = obstacle;
        agent.VaultDoAfter = id;
        agent.VaultUntil = _timing.CurTime + TimeSpan.FromSeconds(surface.ClimbDelay + 2);
        agent.VaultDecision = "vaulting";
        PauseTravelClock(agent, _timing.CurTime);
        return false;
    }

    private void RejectVault(EntityUid uid, CMUExpeditionAgentComponent agent, EntityUid obstacle)
    {
        CancelVault(agent);
        agent.FailedVault = obstacle;
        agent.AvoidVaultUntil = _timing.CurTime + TimeSpan.FromSeconds(8);
        agent.VaultDecision = "vault-blocked-repath";
        agent.Route.Clear();
        agent.RouteDestination = null;
        agent.OrderRoute.Clear();
        agent.NextOrderRoute = _timing.CurTime;
        agent.LastMoveFailed = true;
        agent.MoveUntil = _timing.CurTime;
        _bodyClearCache.Clear();
        _steering.Unregister(uid);
    }
}
