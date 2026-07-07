using System.Globalization;
using System.Numerics;
using Content.Shared.CCVar;
using Content.Shared.Clothing.Components;
using Content.Shared.Movement.Components;
using Robust.Client.GameObjects;
using Robust.Client.GameStates;
using Robust.Client.Physics;
using Robust.Client.Player;
using Robust.Client.Timing;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Client._CMU14.Diagnostics;

public sealed partial class CMURubberbandDiagnosticsSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private IClientGameStateManager _gameStates = default!;
    [Dependency] private IClientGameTiming _timing = default!;
    [Dependency] private ILogManager _log = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private ISawmill _sawmill = default!;

    private bool _enabled = true;
    private float _snapThreshold = 0.75f;
    private int _catchupApplyThreshold = 3;
    private float _logCooldown = 1f;

    private LocalPlayerSnapshot? _lastFrameSnapshot;
    private LocalPlayerSnapshot? _lastStateSnapshot;
    private TimeSpan _nextCatchupLog;
    private TimeSpan _nextSnapLog;
    private uint _stateApplyFrame;
    private int _stateApplicationsThisFrame;
    private GameTick _lastAppliedStateTick;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _log.GetSawmill("cmu.rubberband");

        UpdatesAfter.Add(typeof(TransformSystem));
        UpdatesAfter.Add(typeof(global::Robust.Client.Physics.PhysicsSystem));
        UpdatesOutsidePrediction = true;

        _gameStates.GameStateApplied += OnGameStateApplied;
        _players.LocalPlayerAttached += OnLocalPlayerAttached;
        _players.LocalPlayerDetached += OnLocalPlayerDetached;

        Subs.CVar(_configuration, CCVars.CMURubberbandDiagnosticsEnabled, OnEnabledChanged, true);
        Subs.CVar(_configuration, CCVars.CMURubberbandDiagnosticsSnapThreshold, value => _snapThreshold = Math.Max(0f, value), true);
        Subs.CVar(_configuration, CCVars.CMURubberbandDiagnosticsCatchupApplyThreshold, value => _catchupApplyThreshold = Math.Max(1, value), true);
        Subs.CVar(_configuration, CCVars.CMURubberbandDiagnosticsLogCooldown, value => _logCooldown = Math.Max(0f, value), true);
    }

    public override void Shutdown()
    {
        _gameStates.GameStateApplied -= OnGameStateApplied;
        _players.LocalPlayerAttached -= OnLocalPlayerAttached;
        _players.LocalPlayerDetached -= OnLocalPlayerDetached;

        base.Shutdown();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (!_enabled)
            return;

        if (!TryGetSnapshot(out var current))
        {
            _lastFrameSnapshot = null;
            _lastStateSnapshot = null;
            return;
        }

        if (_lastFrameSnapshot is { } previous &&
            IsComparable(previous, current) &&
            TryGetDistance(previous, current, out var distance) &&
            distance >= GetFrameSnapThreshold(previous, current, frameTime) &&
            CanLog(ref _nextSnapLog))
        {
            LogSnap("frame", previous, current, distance, null, 0);
        }

        _lastFrameSnapshot = current;
    }

    private void OnGameStateApplied(GameStateAppliedArgs args)
    {
        if (!_enabled)
            return;

        if (_stateApplyFrame != _timing.CurFrame)
        {
            _stateApplyFrame = _timing.CurFrame;
            _stateApplicationsThisFrame = 0;
            _lastStateSnapshot = _lastFrameSnapshot;
        }

        _stateApplicationsThisFrame++;

        var stateTickGap = GetStateTickGap(args.AppliedState.ToSequence);

        LocalPlayerSnapshot? current = null;
        if (TryGetSnapshot(out var snapshot))
        {
            current = snapshot;
            if (_lastStateSnapshot is { } previous &&
                IsComparable(previous, snapshot) &&
                TryGetDistance(previous, snapshot, out var distance) &&
                distance >= _snapThreshold &&
                CanLog(ref _nextSnapLog))
            {
                LogSnap("state-applied", previous, snapshot, distance, args, stateTickGap);
            }

            _lastStateSnapshot = snapshot;
        }

        MaybeLogCatchup(args, current, stateTickGap);
        _lastAppliedStateTick = args.AppliedState.ToSequence;
    }

    private void OnLocalPlayerAttached(EntityUid uid)
    {
        ResetSnapshots();

        if (!_enabled || !TryGetSnapshot(out var snapshot))
            return;

        _sawmill.Info($"local-player-attached {FormatSnapshot(snapshot)} {FormatTiming()}");
    }

    private void OnLocalPlayerDetached(EntityUid uid)
    {
        ResetSnapshots();

        if (!_enabled)
            return;

        _sawmill.Info($"local-player-detached ent={uid} player={FormatPlayer()} {FormatTiming()}");
    }

    private void OnEnabledChanged(bool enabled)
    {
        _enabled = enabled;
        ResetSnapshots();

        if (!_enabled)
            return;

        _sawmill.Info($"diagnostics-enabled snapThreshold={F(_snapThreshold)} catchupApplyThreshold={_catchupApplyThreshold} cooldown={F(_logCooldown)}");
    }

    private void MaybeLogCatchup(GameStateAppliedArgs args, LocalPlayerSnapshot? snapshot, uint stateTickGap)
    {
        var applicable = _gameStates.GetApplicableStateCount();
        var catchup = _stateApplicationsThisFrame >= _catchupApplyThreshold;
        var lowBuffer = applicable < _gameStates.MinBufferSize;
        var skippedStateTick = stateTickGap > 1;

        if (!catchup && !lowBuffer && !skippedStateTick)
            return;

        if (!CanLog(ref _nextCatchupLog))
            return;

        _sawmill.Warning($"state-catchup appliesThisFrame={_stateApplicationsThisFrame} lowBuffer={lowBuffer} skippedStateTick={skippedStateTick} {FormatState(args, stateTickGap)} {(snapshot is { } value ? FormatSnapshot(value) : $"player={FormatPlayer()} ent=none")} {FormatTiming()}");
    }

    private bool TryGetSnapshot(out LocalPlayerSnapshot snapshot)
    {
        snapshot = default;

        if (_players.LocalEntity is not { } entity ||
            !Exists(entity) ||
            !TryComp(entity, out TransformComponent? xform))
        {
            return false;
        }

        var map = _transform.GetMapCoordinates(entity, xform: xform);
        if (map.MapId == MapId.Nullspace)
            return false;

        var velocity = Vector2.Zero;
        var physics = "none";
        if (TryComp(entity, out PhysicsComponent? body))
        {
            velocity = body.LinearVelocity;
            physics = $"bodyType={body.BodyType} bodyStatus={body.BodyStatus} awake={body.Awake} predict={body.Predict} canCollide={body.CanCollide} contacts={body.ContactCount}";
        }

        snapshot = new LocalPlayerSnapshot(
            entity,
            GetNetEntity(entity),
            map.MapId,
            xform.GridUid,
            xform.ParentUid,
            map.Position,
            xform.LocalPosition,
            velocity,
            velocity.Length(),
            physics,
            FormatMover(entity),
            FormatRelay(entity),
            _timing.CurTick,
            _timing.LastRealTick,
            _timing.CurFrame,
            _timing.RealTime);

        return true;
    }

    private void LogSnap(
        string reason,
        LocalPlayerSnapshot previous,
        LocalPlayerSnapshot current,
        float distance,
        GameStateAppliedArgs? args,
        uint stateTickGap)
    {
        var state = args != null
            ? FormatState(args, stateTickGap)
            : "state=none";

        _sawmill.Warning($"snap reason={reason} dist={F(distance)} {state} previous={FormatPrevious(previous)} current={FormatSnapshot(current)} {FormatTiming()}");
    }

    private string FormatState(GameStateAppliedArgs args, uint stateTickGap)
    {
        return $"stateFrom={args.AppliedState.FromSequence} stateTo={args.AppliedState.ToSequence} stateTickGap={stateTickGap} payload={args.AppliedState.PayloadSize} detached={args.Detached.Count} bufferApplicable={_gameStates.GetApplicableStateCount()} bufferTotal={_gameStates.StateCount} bufferTarget={_gameStates.TargetBufferSize} bufferMin={_gameStates.MinBufferSize} mergeThreshold={_gameStates.StateBufferMergeThreshold} predictionEnabled={_gameStates.IsPredictionEnabled}";
    }

    private string FormatSnapshot(LocalPlayerSnapshot snapshot)
    {
        return $"player={FormatPlayer()} ping={_players.LocalSession?.Ping ?? -1} ent={ToPrettyString(snapshot.Entity)} net={snapshot.NetEntity} map={snapshot.MapId} grid={FormatEntity(snapshot.GridUid)} parent={FormatEntity(snapshot.ParentUid)} pos={FormatVector(snapshot.MapPosition)} local={FormatVector(snapshot.LocalPosition)} vel={FormatVector(snapshot.LinearVelocity)} speed={F(snapshot.Speed)} snapTick={snapshot.CurTick} snapRealTick={snapshot.LastRealTick} snapFrame={snapshot.Frame} snapTime={F(snapshot.RealTime.TotalSeconds)} physics=\"{snapshot.Physics}\" mover=\"{snapshot.Mover}\" relay=\"{snapshot.Relay}\"";
    }

    private static string FormatPrevious(LocalPlayerSnapshot snapshot)
    {
        return $"ent={snapshot.Entity} net={snapshot.NetEntity} map={snapshot.MapId} grid={FormatEntity(snapshot.GridUid)} parent={FormatEntity(snapshot.ParentUid)} pos={FormatVector(snapshot.MapPosition)} local={FormatVector(snapshot.LocalPosition)} vel={FormatVector(snapshot.LinearVelocity)} speed={F(snapshot.Speed)} tick={snapshot.CurTick} realTick={snapshot.LastRealTick} frame={snapshot.Frame} time={F(snapshot.RealTime.TotalSeconds)}";
    }

    private string FormatTiming()
    {
        return $"timing cur={_timing.CurTick} lastReal={_timing.LastRealTick} lastProcessed={_timing.LastProcessedTick} tickFraction={_timing.TickFraction} inPrediction={_timing.InPrediction} firstPrediction={_timing.IsFirstTimePredicted} applyingState={_timing.ApplyingState} tickAdjust={F(_timing.TickTimingAdjustment)} realFrameMs={F(_timing.RealFrameTime.TotalMilliseconds)} avgFps={F(_timing.FramesPerSecondAvg)}";
    }

    private string FormatMover(EntityUid entity)
    {
        if (!TryComp(entity, out InputMoverComponent? mover))
            return "none";

        return $"held={mover.HeldMoveButtons} wish={FormatVector(mover.WishDir)} sprinting={mover.Sprinting} canMove={mover.CanMove} relative={FormatEntity(mover.RelativeEntity)} relativeRot={F(mover.RelativeRotation.Theta)} targetRot={F(mover.TargetRelativeRotation.Theta)} lastInput={mover.LastInputTick}/{mover.LastInputSubTick}";
    }

    private string FormatRelay(EntityUid entity)
    {
        var relay = TryComp(entity, out RelayInputMoverComponent? relayComp)
            ? FormatEntity(relayComp.RelayEntity)
            : "none";

        var relaySource = TryComp(entity, out MovementRelayTargetComponent? relayTarget)
            ? FormatEntity(relayTarget.Source)
            : "none";

        return $"relayTo={relay} relaySource={relaySource} pilotedByClothing={HasComp<PilotedByClothingComponent>(entity)}";
    }

    private string FormatPlayer()
    {
        var name = _players.LocalSession?.Name ?? "none";
        return $"\"{name.Replace("\"", "\\\"")}\"";
    }

    private static string FormatEntity(EntityUid? entity)
    {
        return entity is { } value && value.IsValid()
            ? value.ToString()
            : "none";
    }

    private static string FormatVector(Vector2 vector)
    {
        return $"({F(vector.X)},{F(vector.Y)})";
    }

    private static string F(float value)
    {
        return float.IsFinite(value)
            ? value.ToString("0.###", CultureInfo.InvariantCulture)
            : value.ToString(CultureInfo.InvariantCulture);
    }

    private static string F(double value)
    {
        return double.IsFinite(value)
            ? value.ToString("0.###", CultureInfo.InvariantCulture)
            : value.ToString(CultureInfo.InvariantCulture);
    }

    private float GetFrameSnapThreshold(LocalPlayerSnapshot previous, LocalPlayerSnapshot current, float frameTime)
    {
        var elapsed = Math.Max(frameTime, (float) _timing.RealFrameTime.TotalSeconds);
        elapsed = Math.Clamp(elapsed, 0f, 0.25f);

        return Math.Max(_snapThreshold, Math.Max(previous.Speed, current.Speed) * elapsed + _snapThreshold);
    }

    private uint GetStateTickGap(GameTick appliedTick)
    {
        if (_lastAppliedStateTick == GameTick.Zero || appliedTick.Value <= _lastAppliedStateTick.Value)
            return 0;

        return appliedTick.Value - _lastAppliedStateTick.Value;
    }

    private bool CanLog(ref TimeSpan nextLog)
    {
        var now = _timing.RealTime;
        if (now < nextLog)
            return false;

        nextLog = now + TimeSpan.FromSeconds(_logCooldown);
        return true;
    }

    private static bool IsComparable(LocalPlayerSnapshot previous, LocalPlayerSnapshot current)
    {
        return previous.Entity == current.Entity && previous.MapId == current.MapId;
    }

    private static bool TryGetDistance(LocalPlayerSnapshot previous, LocalPlayerSnapshot current, out float distance)
    {
        distance = (current.MapPosition - previous.MapPosition).Length();
        return float.IsFinite(distance);
    }

    private void ResetSnapshots()
    {
        _lastFrameSnapshot = null;
        _lastStateSnapshot = null;
        _stateApplicationsThisFrame = 0;
        _lastAppliedStateTick = GameTick.Zero;
    }

    private readonly record struct LocalPlayerSnapshot(
        EntityUid Entity,
        NetEntity NetEntity,
        MapId MapId,
        EntityUid? GridUid,
        EntityUid ParentUid,
        Vector2 MapPosition,
        Vector2 LocalPosition,
        Vector2 LinearVelocity,
        float Speed,
        string Physics,
        string Mover,
        string Relay,
        GameTick CurTick,
        GameTick LastRealTick,
        uint Frame,
        TimeSpan RealTime);
}
