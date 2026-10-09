using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Construction;
using Content.Shared._RMC14.Entrenching;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Content.Shared.Item.ItemToggle.Components;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    [Dependency] private RMCConstructionSystem _construction = default!;

    private bool TryFortify(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (!agent.Entrench || agent.OrderedDestination != null || agent.Home is not { } home ||
            now - agent.LastContact < TimeSpan.FromSeconds(20) || now - agent.LastHit < TimeSpan.FromSeconds(20))
            return false;
        if (agent.FortificationPoint == null)
        {
            if (now < agent.NextWork)
                return false;
            agent.NextWork = now + TimeSpan.FromSeconds(15);
            if (!PlanFortification(uid, agent, agent.GuardAnchor ?? home))
            {
                agent.FortificationDecision = "no-safe-firing-position";
                return false;
            }
        }
        if (agent.FortificationPoint is not { } buildPoint)
            return false;
        var forward = agent.FortificationFacing.ToAngle().RotateVec(new Vector2(0, -1));
        var stance = buildPoint.Offset(-forward * 0.2f);
        if (!_transform.InRange(Transform(uid).Coordinates, stance, 0.1f))
        {
            UpdateMoveProgress(agent, Transform(uid).Coordinates, stance, now);
            if (now - agent.MoveProgressAt > TimeSpan.FromSeconds(2) ||
                !TraversablePassage(uid, Transform(uid).Coordinates, stance))
            {
                CancelWork(uid, agent);
                agent.FortificationPoint = null;
                agent.Home = Transform(uid).Coordinates;
                agent.NextWork = now + TimeSpan.FromSeconds(15);
                agent.FortificationDecision = "build-approach-blocked";
                _steering.Unregister(uid);
                return false;
            }
            agent.FortificationDecision = "moving-to-build-position";
            Move(uid, stance, precise: true, validated: true);
            return true;
        }
        if (agent.WorkDoAfter is { } running)
        {
            var status = _doAfter.GetStatus(running);
            if (status == DoAfterStatus.Running)
                return true;
            agent.WorkDoAfter = null;
            var nearby = new HashSet<EntityUid>();
            var location = _transform.GetMapCoordinates(uid);
            _lookup.GetEntitiesInRange(location.MapId, location.Position, 1.8f, nearby);
            if (agent.WorkBuild && nearby.Any(item => !agent.ExistingFortifications.Contains(item) &&
                (HasComp<DirtMoundComponent>(item) || HasComp<BarricadeComponent>(item)) &&
                _transform.InRange(Transform(item).Coordinates, buildPoint, 0.1f) &&
                Transform(item).LocalRotation.GetCardinalDir() == agent.FortificationFacing))
            {
                agent.Fortifications++;
                agent.Entrench = false;
                agent.Home = stance;
                agent.FortificationDecision = "guard-position-built";
                CancelWork(uid, agent);
                return true;
            }
        }
        if (now < agent.NextWork)
            return true;
        // Do not lower the rifle merely to discover that this floor cannot be dug or that
        // no materials are available. A failed work retry is not a weapon-handling action.
        if (agent.WorkItem == null && !agent.PreparingWork && !CanPrepareFortification(uid, buildPoint))
        {
            agent.NextWork = now + TimeSpan.FromSeconds(15);
            return false;
        }
        agent.NextWork = now + TimeSpan.FromSeconds(1);
        agent.PreparingWork = true;
        _steering.Unregister(uid);
        _physics.SetLinearVelocity(uid, Vector2.Zero);
        if (_guns.TryGetGun(uid, out var rifle))
            _wield.TryUnwield(rifle.Owner, uid);
        // Wielding frees its virtual off-hand on the following tick.
        if (_hands.GetEmptyHandCount(uid) == 0 && agent.WorkItem == null)
            return true;
        if (agent.WorkItem == null)
        {
            var nearby = new HashSet<EntityUid>();
            var point = _transform.GetMapCoordinates(uid);
            _lookup.GetEntitiesInRange(point.MapId, point.Position, 1.5f, nearby);
            foreach (var item in nearby)
            {
                if (TryComp<RMCConstructionItemComponent>(item, out var material) &&
                    material.Buildable?.Any(p => p.Id == "RMCMetalBarricadeBuild") == true &&
                    TryComp<StackComponent>(item, out var stack) && stack.Count >= 4 &&
                    !_containers.IsEntityOrParentInContainer(item) &&
                    _interaction.InRangeUnobstructed(uid, item) && _hands.TryPickupAnyHand(uid, item))
                { agent.WorkItem = item; break; }
            }
            if (agent.WorkItem == null && Supplies(uid, out var supplies))
                foreach (var item in supplies.Container.ContainedEntities.ToArray())
                    if (HasComp<EntrenchingToolComponent>(item) && _hands.TryPickupAnyHand(uid, item))
                    { agent.WorkItem = item; break; }
        }
        if (agent.WorkItem is not { } tool || !Exists(tool))
        {
            CancelWork(uid, agent);
            agent.NextWork = now + TimeSpan.FromSeconds(15);
            return false;
        }
        agent.PreparingWork = false;
        _transform.SetLocalRotation(uid, agent.FortificationFacing.ToAngle());
        agent.ExistingFortifications.Clear();
        var buildLocation = _transform.ToMapCoordinates(buildPoint);
        _lookup.GetEntitiesInRange(buildLocation.MapId, buildLocation.Position, 0.2f, agent.ExistingFortifications);
        var before = TryComp<DoAfterComponent>(uid, out var component) ? component.NextId : (ushort) 0;
        if (TryComp<RMCConstructionItemComponent>(tool, out var metal))
        {
            agent.WorkBuild = true;
            _construction.Build((tool, metal), uid, "RMCMetalBarricadeBuild", 1);
        }
        else if (TryComp<EntrenchingToolComponent>(tool, out var shovel))
        {
            if (HasComp<ItemToggleComponent>(tool) && !_itemToggle.IsActivated(tool) && !_itemToggle.TryActivate(tool, uid))
            {
                CancelWork(uid, agent);
                return false;
            }
            agent.WorkBuild = shovel.TotalLayers >= shovel.MoundCost;
            var interact = new AfterInteractEvent(uid, tool, null, buildPoint, true);
            RaiseLocalEvent(tool, interact);
        }
        agent.FortificationDecision = agent.WorkBuild ? "building-oriented-cover" : "digging-material";
        if (TryComp<DoAfterComponent>(uid, out component) && component.NextId != before)
            agent.WorkDoAfter = new DoAfterId(uid, before);
        else
        {
            CancelWork(uid, agent);
            agent.FortificationPoint = null;
            agent.FortificationDecision = "native-build-rejected";
            agent.NextWork = now + TimeSpan.FromSeconds(15);
        }
        return true;
    }

    private bool CanPrepareFortification(EntityUid uid, EntityCoordinates buildPoint)
    {
        var point = _transform.GetMapCoordinates(uid);
        var nearby = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(point.MapId, point.Position, 1.5f, nearby);
        if (nearby.Any(item => TryComp<RMCConstructionItemComponent>(item, out var material) &&
            material.Buildable?.Any(p => p.Id == "RMCMetalBarricadeBuild") == true &&
            TryComp<StackComponent>(item, out var stack) && stack.Count >= 4 &&
            !_containers.IsEntityOrParentInContainer(item) && _interaction.InRangeUnobstructed(uid, item)))
            return true;
        if (!Supplies(uid, out var supplies))
            return false;
        var diggable = _turf.TryGetTileRef(buildPoint, out var tile) &&
            _turf.GetContentTileDefinition(tile.Value).CanDig;
        return supplies.Container.ContainedEntities.Any(item => TryComp<EntrenchingToolComponent>(item, out var shovel) &&
            (diggable || shovel.TotalLayers >= shovel.MoundCost));
    }

    private bool PlanFortification(EntityUid uid, CMUExpeditionAgentComponent agent, EntityCoordinates home)
    {
        var center = new EntityCoordinates(home.EntityId, new Vector2(MathF.Floor(home.X) + 0.5f, MathF.Floor(home.Y) + 0.5f));
        var best = float.MinValue;
        foreach (var point in NearbySquadPositions(center, 2))
        foreach (var facing in new[] { Direction.North, Direction.East, Direction.South, Direction.West })
        {
            if (agent.GuardFacing != null && agent.GuardFacing != facing)
                continue;
            var forward = facing.ToAngle().RotateVec(new Vector2(0, -1));
            var side = new Vector2(-forward.Y, forward.X);
            var stance = point.Offset(-forward * 0.2f);
            // Keep a rear escape and a lateral firing exit. Never seal a doorway or
            // build a closed ring around a squad. Native construction validates the tile.
            if (Reserved(uid, stance) || !CanPrepareFortification(uid, point) ||
                !TraversablePassage(uid, Transform(uid).Coordinates, stance) ||
                !TraversablePassage(uid, stance, stance.Offset(-forward * 1.5f)) ||
                !TraversablePassage(uid, stance, stance.Offset(side * 1.5f)) &&
                !TraversablePassage(uid, stance, stance.Offset(-side * 1.5f)) ||
                !_construction.CanBuildAt(point, "AU14DirtMound", out _, direction: facing, user: uid))
                continue;
            var lane = 0;
            for (var range = 2; range <= 8; range += 2)
            {
                if (!FiringLaneClear(uid, stance, point.Offset(forward * range)))
                    break;
                lane = range;
            }
            if (lane < 4)
                continue;
            var score = lane - Vector2.Distance(home.Position, stance.Position) * 2;
            if (score <= best)
                continue;
            best = score;
            agent.FortificationPoint = point;
            agent.FortificationFacing = facing;
        }
        if (agent.FortificationPoint == null)
            return false;
        agent.Home = agent.FortificationPoint.Value.Offset(-agent.FortificationFacing.ToAngle().RotateVec(new Vector2(0, -1)) * 0.2f);
        agent.NextWork = _timing.CurTime;
        return true;
    }

    private void CancelWork(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        if (agent.WorkDoAfter is { } work && _doAfter.GetStatus(work) == DoAfterStatus.Running)
            _doAfter.Cancel(work);
        agent.WorkDoAfter = null;
        if (agent.WorkItem is { } item && Exists(item))
        {
            if (!Supplies(uid, out var supplies) || !_hands.TryDropIntoContainer(uid, item, supplies.Container))
                _hands.TryDrop(uid, item);
        }
        agent.WorkItem = null;
        agent.PreparingWork = false;
    }
}
