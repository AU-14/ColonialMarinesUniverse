using System.Diagnostics.CodeAnalysis;
using Content.Server.Administration.Logs;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.Stack;
using Content.Server.Tools;
using Content.Shared.Database;
using Content.Shared.Interaction;
using Content.Shared.NodeContainer;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using JetBrains.Annotations;

namespace Content.Server.Power.EntitySystems;

// Runs a cable by hand between two devices with the coil, using the stack of cable, and lets a link be taken off with a verb.
[UsedImplicitly]
public sealed class DirectCableLinkerSystem : EntitySystem
{
    [Dependency] private readonly NodeContainerSystem _nodeContainer = default!;
    [Dependency] private readonly NodeGroupSystem _nodeGroupSystem = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly ToolSystem _toolSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly Content.Server.CMU14.Power.DirectCableVisualSystem _visual = default!;
    [Dependency] private readonly Content.Server.CMU14.Power.DirectPowerSystem _direct = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DirectCableLinkerComponent, AfterInteractEvent>(OnAfterInteract, before: new[] { typeof(CableSystem) });
        SubscribeLocalEvent<NodeContainerComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    private void OnAfterInteract(Entity<DirectCableLinkerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        var linker = ent.Comp;

        if (!TryGetDirectNode(target, out _, out var targetNodeName))
            return;

        if (linker.PendingSource is not { } source || !Exists(source))
        {
            linker.PendingSource = target;
            linker.PendingNodeName = targetNodeName;
            _popup.PopupEntity(Loc.GetString("direct-cable-linker-first-point", ("target", target)), args.User, args.User);
            args.Handled = true;
            return;
        }

        args.Handled = true;

        if (source == target)
        {
            linker.PendingSource = null;
            linker.PendingNodeName = null;
            _popup.PopupEntity(Loc.GetString("direct-cable-linker-cancelled"), args.User, args.User);
            return;
        }

        // A device may have several nodes for a cable by hand (a transformer has two): the pair is the two that carry the same network.
        if (!TryFindPair(source, target, out var sourceNode, out var targetNode))
        {
            if (!TryGetDirectNode(source, out _, out _))
            {
                linker.PendingSource = null;
                linker.PendingNodeName = null;
                _popup.PopupEntity(Loc.GetString("direct-cable-linker-source-lost"), args.User, args.User);
                return;
            }

            _popup.PopupEntity(Loc.GetString("direct-cable-linker-incompatible"), args.User, args.User);
            return;
        }

        if (sourceNode.DirectLinks.Contains(targetNode))
        {
            linker.PendingSource = null;
            linker.PendingNodeName = null;
            _popup.PopupEntity(Loc.GetString("direct-cable-linker-already-linked"), args.User, args.User);
            return;
        }

        var sourceCoords = _transform.GetMapCoordinates(source);
        var targetCoords = _transform.GetMapCoordinates(target);
        if (sourceCoords.MapId != targetCoords.MapId ||
            (sourceCoords.Position - targetCoords.Position).Length() > linker.MaxDistance)
        {
            _popup.PopupEntity(Loc.GetString("direct-cable-linker-too-far"), args.User, args.User);
            return;
        }

        if (TryComp<StackComponent>(ent.Owner, out var stack) && !_stack.TryUse((ent.Owner, stack), 1))
        {
            linker.PendingSource = null;
            linker.PendingNodeName = null;
            _popup.PopupEntity(Loc.GetString("direct-cable-linker-not-enough-cable"), args.User, args.User);
            return;
        }

        sourceNode.DirectLinks.Add(targetNode);
        targetNode.DirectLinks.Add(sourceNode);
        _nodeGroupSystem.QueueReflood(sourceNode);
        _nodeGroupSystem.QueueReflood(targetNode);
        _visual.Link(source, target, sourceNode.NodeGroupID.ToString());
        _direct.Linked(source, target);

        _adminLogger.Add(LogType.Construction, LogImpact.Low,
            $"{ToPrettyString(args.User):player} connected {ToPrettyString(source):source} to {ToPrettyString(target):target} with a direct power cable");

        _popup.PopupEntity(Loc.GetString("direct-cable-linker-success", ("source", source), ("target", target)), args.User, args.User);

        linker.PendingSource = null;
        linker.PendingNodeName = null;
    }

    private bool TryFindPair(EntityUid first, EntityUid second, [NotNullWhen(true)] out Node? firstNode, [NotNullWhen(true)] out Node? secondNode)
    {
        firstNode = null;
        secondNode = null;

        if (!TryComp<NodeContainerComponent>(first, out var a) || !TryComp<NodeContainerComponent>(second, out var b))
            return false;

        Node? fallbackA = null;
        Node? fallbackB = null;
        foreach (var left in a.Nodes.Values)
        {
            if (left.Connection == NodeConnectorMode.Adjacent || !left.Connectable(EntityManager))
                continue;

            foreach (var right in b.Nodes.Values)
            {
                if (right.Connection == NodeConnectorMode.Adjacent || !right.Connectable(EntityManager) || left.NodeGroupID != right.NodeGroupID)
                    continue;

                // The pair that is not joined yet is the one: with a pair already joined the next free one is taken.
                if (!left.DirectLinks.Contains(right))
                {
                    firstNode = left;
                    secondNode = right;
                    return true;
                }

                fallbackA ??= left;
                fallbackB ??= right;
            }
        }

        firstNode = fallbackA;
        secondNode = fallbackB;
        return firstNode != null && secondNode != null;
    }

    private bool TryGetDirectNode(EntityUid uid, [NotNullWhen(true)] out Node? node, out string nodeName, string? preferredName = null)
    {
        node = null;
        nodeName = string.Empty;

        if (!TryComp<NodeContainerComponent>(uid, out var container))
            return false;

        if (preferredName != null &&
            _nodeContainer.TryGetNode<Node>(container, preferredName, out var preferred) &&
            preferred.Connection != NodeConnectorMode.Adjacent &&
            preferred.Connectable(EntityManager))
        {
            node = preferred;
            nodeName = preferredName;
            return true;
        }

        foreach (var (name, candidate) in container.Nodes)
        {
            if (candidate.Connection == NodeConnectorMode.Adjacent)
                continue;

            if (!candidate.Connectable(EntityManager))
                continue;

            node = candidate;
            nodeName = name;
            return true;
        }

        return false;
    }

    private void OnGetVerbs(Entity<NodeContainerComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var hasDirectLinks = false;
        foreach (var node in ent.Comp.Nodes.Values)
        {
            if (node.DirectLinks.Count > 0)
            {
                hasDirectLinks = true;
                break;
            }
        }

        if (!hasDirectLinks)
            return;

        var canCut = args.Using is { } used && _toolSystem.HasQuality(used, SharedToolSystem.CutQuality);

        var verb = new AlternativeVerb
        {
            Text = Loc.GetString("direct-cable-linker-disconnect-verb"),
            Disabled = !canCut,
            Message = canCut ? null : Loc.GetString("direct-cable-linker-disconnect-verb-needs-cutters"),
            Act = () => DisconnectAll(ent)
        };

        args.Verbs.Add(verb);
    }

    public void DisconnectAll(Entity<NodeContainerComponent> ent)
    {
        var former = new List<EntityUid>();
        foreach (var node in ent.Comp.Nodes.Values)
        {
            if (node.DirectLinks.Count == 0)
                continue;

            foreach (var linked in node.DirectLinks)
            {
                linked.DirectLinks.Remove(node);
                _nodeGroupSystem.QueueReflood(linked);
                _visual.Unlink(ent.Owner, linked.Owner);
                former.Add(linked.Owner);
            }

            node.DirectLinks.Clear();
            _nodeGroupSystem.QueueReflood(node);
        }

        // The cables are gone from the nodes: only now the machines that were on them let their providers go.
        foreach (var other in former)
            _direct.Unlinked(ent.Owner, other);

        _popup.PopupEntity(Loc.GetString("direct-cable-linker-disconnected"), ent.Owner);
    }
}
