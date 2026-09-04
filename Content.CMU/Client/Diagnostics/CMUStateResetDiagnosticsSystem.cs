using System.Linq;
using System.Text;
using Content.Shared.GameTicking;
using Robust.Client.GameStates;
using Robust.Shared.Log;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Diagnostics;

/// <summary>
/// Records the content callback responsible if an entity is reparented while the engine is detaching a hierarchy for
/// a partial state reset. Reparenting at that point can invalidate the child collection being enumerated.
/// </summary>
public sealed partial class CMUStateResetDiagnosticsSystem : EntitySystem
{
    [Dependency] private IClientGameStateManager _gameStates = default!;
    [Dependency] private ILogManager _log = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const string PartialStateResetFrame = "ClientGameStateManager.PartialStateReset";
    private const int MaxChildSnapshotCount = 32;
    private const int MaxHierarchyDepth = 16;
    private const int MaxTerminationHistory = 8;

    private ISawmill _sawmill = default!;
    private readonly Queue<string> _terminationHistory = new();
    private uint _terminationCount;
    private uint _terminationFrame;
    private string? _terminationTrace;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _log.GetSawmill("cmu.state_reset");
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<EntityTerminatingEvent>(OnEntityTerminating);
        SubscribeLocalEvent<TransformComponent, EntParentChangedMessage>(OnParentChanged);

        EntityManager.ComponentAdded += OnComponentAdded;
        EntityManager.EntityAdded += OnEntityAdded;
        _gameStates.GameStateApplied += OnGameStateApplied;
    }

    public override void Shutdown()
    {
        EntityManager.ComponentAdded -= OnComponentAdded;
        EntityManager.EntityAdded -= OnEntityAdded;
        _gameStates.GameStateApplied -= OnGameStateApplied;
        base.Shutdown();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        FlushFailedTerminationAttempt();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent _)
    {
        _sawmill.Info(
            "[CMU-STATE-RESET-EVENT-ORDER] RoundRestartCleanupEvent reached content. " +
            $"Frame: {_timing.CurFrame}; tick: {_timing.CurTick}; applying state: {_timing.ApplyingState}.");
    }

    private void OnGameStateApplied(GameStateAppliedArgs args)
    {
        if (_terminationHistory.Count == 0)
            return;

        _sawmill.Info(
            "[CMU-STATE-RESET-DELETE-CONTEXT] PartialStateReset deletion pass completed successfully. " +
            $"Discarding {_terminationCount} buffered termination records. Applied state: " +
            $"{args.AppliedState.FromSequence} to {args.AppliedState.ToSequence}.");
        ClearTerminationHistory();
    }

    private void OnParentChanged(Entity<TransformComponent> ent, ref EntParentChangedMessage args)
    {
        if (!args.Transform.ParentUid.IsValid() || !TryGetPartialStateResetTrace(out var trace))
            return;

        var oldParent = args.OldParent;
        var newParent = args.Transform.ParentUid;

        _sawmill.Error(
            "[CMU-STATE-RESET-REPARENT] An entity was attached to a valid parent while PartialStateReset was " +
            "detaching entity hierarchies. This can invalidate the engine child collection and cause the " +
            "'Collection was modified' exception.\n" +
            $"Frame: {_timing.CurFrame}; tick: {_timing.CurTick}\n" +
            $"Entity: {FormatEntityDetails(ent.Owner)}\n" +
            $"Old parent: {FormatEntityDetails(oldParent)}\n" +
            $"Old parent's current children: {FormatChildren(oldParent)}\n" +
            $"New parent: {FormatEntityDetails(newParent)}\n" +
            $"New parent's current children: {FormatChildren(newParent)}\n" +
            $"Hierarchy after reparent: {FormatHierarchy(ent.Owner)}\n" +
            $"Full reparent caller stack:\n{trace}");
    }

    private void OnEntityAdded(Entity<MetaDataComponent> ent)
    {
        if (!TryGetPartialStateResetTrace(out var trace))
            return;

        _sawmill.Error(
            "[CMU-STATE-RESET-ENTITY-ADD] An entity was created from inside PartialStateReset. " +
            "Entity creation can mutate the hierarchy or lifecycle collections being enumerated.\n" +
            $"Frame: {_timing.CurFrame}; tick: {_timing.CurTick}\n" +
            $"Entity: {FormatEntityDetails(ent.Owner)}\n" +
            $"Full entity creation caller stack:\n{trace}");
    }

    private void OnComponentAdded(AddedComponentEventArgs args)
    {
        if (!TryGetPartialStateResetTrace(out var trace))
            return;

        _sawmill.Error(
            "[CMU-STATE-RESET-COMPONENT-ADD] A component was added from inside PartialStateReset. " +
            "Component creation during deletion may expose the content callback mutating reset state.\n" +
            $"Frame: {_timing.CurFrame}; tick: {_timing.CurTick}\n" +
            $"Component: {args.ComponentType.Name} ({args.ComponentType.Type.FullName})\n" +
            $"Owner: {FormatEntityDetails(args.BaseArgs.Owner)}\n" +
            $"Full component creation caller stack:\n{trace}");
    }

    private void OnEntityTerminating(ref EntityTerminatingEvent args)
    {
        if (_terminationFrame != _timing.CurFrame)
        {
            FlushFailedTerminationAttempt();
            _terminationFrame = _timing.CurFrame;
            _terminationTrace = null;
        }

        if (_terminationTrace == null)
        {
            if (!TryGetPartialStateResetTrace(out var trace))
                return;

            _terminationTrace = trace;
        }

        _terminationCount++;
        if (_terminationHistory.Count >= MaxTerminationHistory)
            _terminationHistory.Dequeue();

        _terminationHistory.Enqueue(FormatEntityDetails(args.Entity.Owner));
    }

    private void FlushFailedTerminationAttempt()
    {
        if (_terminationHistory.Count == 0)
            return;

        var history = string.Join("\n", _terminationHistory.Select((entry, index) => $"  {index + 1}. {entry}"));
        _sawmill.Error(
            "[CMU-STATE-RESET-DELETE-CONTEXT] PartialStateReset entered entity termination but did not reach " +
            "GameStateApplied before the frame ended. These are the last terminating entities before the aborted " +
            "deletion pass.\n" +
            $"Frame: {_terminationFrame}; tick: {_timing.CurTick}; terminations observed: {_terminationCount}; " +
            $"retained: {_terminationHistory.Count}\n" +
            $"Last terminating entities:\n{history}\n" +
            $"PartialStateReset termination stack:\n{_terminationTrace}");
        ClearTerminationHistory();
    }

    private void ClearTerminationHistory()
    {
        _terminationHistory.Clear();
        _terminationCount = 0;
        _terminationTrace = null;
    }

    private bool TryGetPartialStateResetTrace(out string trace)
    {
        trace = string.Empty;
        if (!_timing.ApplyingState)
            return false;

        trace = Environment.StackTrace;
        return trace.Contains(PartialStateResetFrame, StringComparison.Ordinal);
    }

    private string FormatEntity(EntityUid? uid)
    {
        if (uid is not { } entity || !entity.IsValid())
            return "<null>";

        return Exists(entity) ? ToPrettyString(entity) : $"{entity} <deleted>";
    }

    private string FormatEntityDetails(EntityUid? uid)
    {
        if (uid is not { } entity || !entity.IsValid())
            return "<null>";

        if (!Exists(entity))
            return $"{entity} <deleted>";

        var details = new StringBuilder(FormatEntity(entity));
        details.Append($"; client-side: {IsClientSide(entity)}");

        if (TryComp(entity, out MetaDataComponent? metadata))
        {
            details.Append(
                $"; net entity: {metadata.NetEntity}; prototype: {metadata.EntityPrototype?.ID ?? "<none>"}; " +
                $"life stage: {metadata.EntityLifeStage}; entity modified: {metadata.EntityLastModifiedTick}; " +
                $"last state: {metadata.LastStateApplied}; last component removed: {metadata.LastComponentRemoved}");
        }

        if (TryComp(entity, out TransformComponent? xform))
        {
            details.Append(
                $"; parent: {FormatEntity(xform.ParentUid)}; children: {xform.ChildCount}; " +
                $"map: {xform.MapUid}; grid: {xform.GridUid}; anchored: {xform.Anchored}; " +
                $"local position: {xform.LocalPosition}");
        }

        details.Append($"; components: {FormatComponents(entity)}");
        return details.ToString();
    }

    private string FormatComponents(EntityUid uid)
    {
        if (!Exists(uid))
            return "<entity does not exist>";

        try
        {
            return string.Join(", ", AllComps(uid)
                .Select(component => component.GetType().Name)
                .Order());
        }
        catch (Exception exception)
        {
            return $"<component inspection failed: {exception}>";
        }
    }

    private string FormatChildren(EntityUid? uid)
    {
        if (uid is not { } entity || !TryComp(entity, out TransformComponent? xform))
            return "<no transform>";

        try
        {
            var children = new List<string>();
            var enumerator = xform.ChildEnumerator;
            while (children.Count < MaxChildSnapshotCount && enumerator.MoveNext(out var child))
            {
                children.Add(FormatEntity(child));
            }

            enumerator.Dispose();
            var suffix = xform.ChildCount > children.Count
                ? $", ... {xform.ChildCount - children.Count} more"
                : string.Empty;
            return $"[{string.Join(", ", children)}{suffix}]";
        }
        catch (Exception exception)
        {
            return $"<child inspection failed: {exception}>";
        }
    }

    private string FormatHierarchy(EntityUid uid)
    {
        var hierarchy = new StringBuilder();
        var visited = new HashSet<EntityUid>();
        var current = uid;

        for (var depth = 0; depth < MaxHierarchyDepth; depth++)
        {
            if (!visited.Add(current))
            {
                hierarchy.Append($"<cycle to {FormatEntity(current)}>");
                return hierarchy.ToString();
            }

            if (depth != 0)
                hierarchy.Append(" -> ");

            hierarchy.Append(FormatEntity(current));

            if (!TryComp(current, out TransformComponent? xform) || !xform.ParentUid.IsValid())
                return hierarchy.ToString();

            current = xform.ParentUid;
        }

        hierarchy.Append(" -> <depth limit reached>");
        return hierarchy.ToString();
    }
}
