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
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ILogManager _log = default!;

    private const string PartialStateResetFrame = "ClientGameStateManager.PartialStateReset";
    private const int MaxHierarchyDepth = 16;

    private ISawmill _sawmill = default!;
    private bool _roundResetPending;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _log.GetSawmill("cmu.state_reset");
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<TransformComponent, EntParentChangedMessage>(OnParentChanged);
        _gameStates.GameStateApplied += OnGameStateApplied;
    }

    public override void Shutdown()
    {
        _gameStates.GameStateApplied -= OnGameStateApplied;
        base.Shutdown();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent _)
    {
        _roundResetPending = true;
        _sawmill.Info("[CMU-STATE-RESET-DIAGNOSTICS] Armed hierarchy diagnostics for the incoming full state reset.");
    }

    private void OnGameStateApplied(GameStateAppliedArgs args)
    {
        if (!_roundResetPending || args.AppliedState.FromSequence != GameTick.Zero)
            return;

        _roundResetPending = false;
        _sawmill.Info("[CMU-STATE-RESET-DIAGNOSTICS] Full state reset completed without an unexpected reparent.");
    }

    private void OnParentChanged(Entity<TransformComponent> ent, ref EntParentChangedMessage args)
    {
        if (!_roundResetPending || !_timing.ApplyingState || !args.Transform.ParentUid.IsValid())
            return;

        var trace = Environment.StackTrace;
        if (!trace.Contains(PartialStateResetFrame, StringComparison.Ordinal))
            return;

        var oldParent = FormatEntity(args.OldParent);
        var newParent = FormatEntity(args.Transform.ParentUid);
        var entityComponents = FormatComponents(ent.Owner);
        var parentComponents = FormatComponents(args.Transform.ParentUid);
        var hierarchy = FormatHierarchy(ent.Owner);
        var oldParentChildren = GetChildCount(args.OldParent);
        var newParentChildren = GetChildCount(args.Transform.ParentUid);

        _sawmill.Error(
            "[CMU-STATE-RESET-REPARENT] An entity was attached to a valid parent while PartialStateReset was " +
            "detaching entity hierarchies. This can invalidate the engine child collection and cause the " +
            "'Collection was modified' exception.\n" +
            $"Entity: {ToPrettyString(ent.Owner)}\n" +
            $"Old parent: {oldParent} (children now: {oldParentChildren})\n" +
            $"New parent: {newParent} (children now: {newParentChildren})\n" +
            $"Map UID: {args.Transform.MapUid}; Grid UID: {args.Transform.GridUid}; Entity children: {args.Transform.ChildCount}\n" +
            $"Hierarchy: {hierarchy}\n" +
            $"Entity components: {entityComponents}\n" +
            $"New parent components: {parentComponents}\n" +
            $"Full reparent caller stack:\n{trace}");
    }

    private string FormatEntity(EntityUid? uid)
    {
        if (uid is not { } entity || !entity.IsValid())
            return "<null>";

        return Exists(entity) ? ToPrettyString(entity) : $"{entity} <deleted>";
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

    private int GetChildCount(EntityUid? uid)
    {
        return uid is { } entity && TryComp(entity, out TransformComponent? xform)
            ? xform.ChildCount
            : -1;
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
