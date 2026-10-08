using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Construction;
using Content.Shared._RMC14.Entrenching;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Stacks;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    [Dependency] private RMCConstructionSystem _construction = default!;

    private bool TryFortify(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (!agent.Entrench || agent.Home is not { } home || !_transform.InRange(Transform(uid).Coordinates, home, 0.6f) ||
            now - agent.LastContact < TimeSpan.FromSeconds(20) || now - agent.LastHit < TimeSpan.FromSeconds(20))
            return false;
        if (agent.WorkDoAfter is { } running)
        {
            var status = _doAfter.GetStatus(running);
            if (status == DoAfterStatus.Running)
                return true;
            agent.WorkDoAfter = null;
            if (status == DoAfterStatus.Finished && agent.WorkBuild)
            {
                agent.Fortifications++;
                agent.Entrench = false;
                CancelWork(uid, agent);
                return false;
            }
        }
        if (now < agent.NextWork)
            return agent.WorkItem != null;
        agent.NextWork = now + TimeSpan.FromSeconds(1);
        _steering.Unregister(uid);
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
                    _interaction.InRangeUnobstructed(uid, item) && _hands.TryPickupAnyHand(uid, item))
                { agent.WorkItem = item; break; }
            }
            if (agent.WorkItem == null && Supplies(uid, out var supplies))
                foreach (var item in supplies.Container.ContainedEntities.ToArray())
                    if (HasComp<EntrenchingToolComponent>(item) && _hands.TryPickupAnyHand(uid, item))
                    { agent.WorkItem = item; break; }
        }
        if (agent.WorkItem is not { } tool || !Exists(tool))
            return false;
        var before = TryComp<DoAfterComponent>(uid, out var component) ? component.NextId : (ushort) 0;
        if (TryComp<RMCConstructionItemComponent>(tool, out var metal))
        {
            agent.WorkBuild = true;
            _construction.Build((tool, metal), uid, "RMCMetalBarricadeBuild", 1);
        }
        else if (TryComp<EntrenchingToolComponent>(tool, out var shovel))
        {
            agent.WorkBuild = shovel.TotalLayers >= shovel.MoundCost;
            var target = Transform(uid).Coordinates.Offset(new Vector2(0, 1));
            var interact = new AfterInteractEvent(uid, tool, null, target, true);
            RaiseLocalEvent(tool, interact);
        }
        if (TryComp<DoAfterComponent>(uid, out component) && component.NextId != before)
            agent.WorkDoAfter = new DoAfterId(uid, before);
        else
        {
            CancelWork(uid, agent);
            agent.NextWork = now + TimeSpan.FromSeconds(15);
        }
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
    }
}
