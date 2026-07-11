using System.Numerics;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Emote;
using Content.Shared._RMC14.Pulling;
using Content.Shared.Mobs;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Pulling.Events;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._RMC14.Xenonids.Charge.CursorCharge;

public sealed partial class XenoChargerMovementSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedRMCEmoteSystem _rmcEmote = default!;
    [Dependency] private XenoChargerCollisionSystem _collision = default!;
    [Dependency] private SharedRMCFlammableSystem _flammable = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private RMCPullingSystem _rmcPulling = default!;

    private EntityQuery<PhysicsComponent> _physicsQuery;

    public override void Initialize()
    {
        _physicsQuery = GetEntityQuery<PhysicsComponent>();

        SubscribeNetworkEvent<XenoCursorSteeringMessage>(OnCursorSteeringMessage);
        SubscribeLocalEvent<XenoChargerStateComponent, MoveInputEvent>(OnMoveInput);
        SubscribeLocalEvent<XenoChargerStateComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<XenoChargerStateComponent, StartPullAttemptEvent>(OnStartPullAttempt);
        SubscribeLocalEvent<XenoChargerStateComponent, PullAttemptEvent>(OnPullAttempt);

    }

    // -------------------------------------------------------------------------
    // Public transition API — the only place MoveState changes
    // -------------------------------------------------------------------------

    public void StartCharge(EntityUid xeno)
    {
        _rmcPulling.TryStopAllPullsFromAndOn(xeno);

        var stateComp = EnsureComp<XenoChargerStateComponent>(xeno);

        var currentRotation = _transform.GetWorldRotation(xeno);
        var currentHeading = currentRotation.ToWorldVec().ToAngle();
        stateComp.TargetHeading = currentHeading;
        stateComp.CurrentHeading = currentHeading;

        stateComp.MoveState = XenoChargerMoveState.Charging;
        stateComp.Stage = 0;
        stateComp.DistanceTraveled = 0f;
        stateComp.SoundDistanceAccumulator = 0f;
        stateComp.HitEntities.Clear();

        Dirty(xeno, stateComp);
    }

    public void StartLunge(EntityUid uid)
    {
        _rmcPulling.TryStopAllPullsFromAndOn(uid);

        if (!TryComp(uid, out XenoChargerComponent? xeno))
            return;

        // State must exist — can't lunge without having charged first.
        if (!TryComp(uid, out XenoChargerStateComponent? state))
            return;

        var direction = state.MoveState == XenoChargerMoveState.Charging
            ? state.CurrentHeading.ToVec()
            : state.TargetHeading.ToVec();

        if (_physicsQuery.TryGetComponent(uid, out var physics))
            _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);

        state.MoveState = XenoChargerMoveState.Lunging;
        state.LungeDirection = direction;
        state.LungeDistanceRemaining = xeno.LungeDistance + state.Stage * xeno.LungeDistancePerStage;
        state.HitEntities.Clear();


        _stun.TryParalyze(uid, xeno.LungeSelfStunDuration, false);
        Dirty(uid, state);
    }

    public void ResetToIdle(EntityUid uid, bool completed = false)
    {
        if (_physicsQuery.TryGetComponent(uid, out var physics))
            _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);

        var ev = new XenoChargerResetEvent(completed);
        RaiseLocalEvent(uid, ref ev);

        RemComp<XenoChargerStateComponent>(uid);
    }

    // -------------------------------------------------------------------------
    // Update — single velocity write per tick
    // -------------------------------------------------------------------------

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<XenoChargerStateComponent, XenoChargerComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var stateComp, out var xenoComp, out var physics))
        {
            switch (stateComp.MoveState)
            {
                case XenoChargerMoveState.Charging:
                    UpdateCharging((uid, stateComp), (uid, xenoComp), physics, frameTime);
                    break;
                case XenoChargerMoveState.Lunging:
                    UpdateLunging((uid, stateComp), (uid, xenoComp), physics, frameTime);
                    break;
            }
        }

        if (_net.IsServer)
            _collision.ProcessHits();
    }

    private void UpdateCharging(Entity<XenoChargerStateComponent> state, Entity<XenoChargerComponent> xeno, PhysicsComponent physics, float frameTime)
    {
        var stateComp = state.Comp;
        var xenoComp = xeno.Comp;

        if (!stateComp.HeadingInitialized)
            return;

        // Speed scales up with stage.
        var speed = xenoComp.BaseSpeed + stateComp.Stage * xenoComp.SpeedPerStage;
        var vel = stateComp.CurrentHeading.ToVec() * speed;

        // Accumulate distance and increment stage.
        var distThisFrame = speed * frameTime;
        stateComp.DistanceTraveled += distThisFrame;


        if (stateComp.Stage < xenoComp.MaxStage && stateComp.DistanceTraveled >= xenoComp.DistancePerStage)
        {
            stateComp.Stage++;
            stateComp.DistanceTraveled -= xenoComp.DistancePerStage;

            if (stateComp.Stage == xenoComp.MaxStage)
                _rmcEmote.TryEmoteWithChat(xeno, "XenoRoar", cooldown: TimeSpan.FromSeconds(20));
        }

        // Turn rate scales down as stage increases.
        var stageRatio = stateComp.Stage / (float)xenoComp.MaxStage;
        var maxTurnRate = MathHelper.Lerp(xenoComp.BaseTurnRate, xenoComp.MinTurnRate, stageRatio);

        var angleDelta = (stateComp.TargetHeading - stateComp.CurrentHeading).Reduced();
        var delta = angleDelta.Theta;
        if (delta > Math.PI) delta -= 2 * Math.PI;
        else if (delta < -Math.PI) delta += 2 * Math.PI;

        var turnAmount = (float)Math.Clamp(delta, -maxTurnRate * frameTime, maxTurnRate * frameTime);
        stateComp.CurrentHeading = new Angle(stateComp.CurrentHeading.Theta + turnAmount);



        _physics.SetAwake((xeno, physics), true);
        _physics.SetLinearVelocity(xeno, vel, body: physics);

        _transform.SetWorldRotation(xeno, GetWorldRotation(stateComp.CurrentHeading));

        // Stomp sound.
        stateComp.SoundDistanceAccumulator += distThisFrame;
        var soundInterval = xenoComp.SoundEveryDistance / (1f + stateComp.Stage * 0.15f);
        if (stateComp.SoundDistanceAccumulator >= soundInterval && _net.IsServer && xenoComp.ChargeSound != null)
        {
            stateComp.SoundDistanceAccumulator = 0f;
            _audio.PlayPvs(xenoComp.ChargeSound, xeno);
            //doing fire stack clearing increments here too.
            _flammable.AdjustStacks(xeno.Owner, -xenoComp.FireStacksCleared);
        }

        Dirty(xeno, stateComp);
    }

    private void UpdateLunging(Entity<XenoChargerStateComponent> state, Entity<XenoChargerComponent> xeno, PhysicsComponent physics, float frameTime)
    {
        var stateComp = state.Comp;
        var xenoComp = xeno.Comp;

        var speed = xenoComp.LungeSpeed + stateComp.Stage * xenoComp.LungeSpeedPerStage;

        _physics.SetAwake((xeno, physics), true);
        _physics.SetLinearVelocity(xeno, stateComp.LungeDirection * speed, body: physics);
        _transform.SetWorldRotation(xeno, stateComp.LungeDirection.ToWorldAngle());

        stateComp.LungeDistanceRemaining -= speed * frameTime;
        Dirty(xeno, stateComp);

        if (stateComp.LungeDistanceRemaining <= 0f)
            ResetToIdle(xeno.Owner, completed: true);
    }

    // -------------------------------------------------------------------------
    // Input / network
    // -------------------------------------------------------------------------

    private void OnCursorSteeringMessage(XenoCursorSteeringMessage msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } controlled)
            return;

        if (!TryComp(controlled, out XenoChargerStateComponent? comp))
            return;

        if (comp.MoveState == XenoChargerMoveState.Idle)
            return;

        var playerPos = _transform.GetMapCoordinates(controlled).Position;
        var diff = msg.CursorWorldPosition - playerPos;
        if (diff.LengthSquared() < 0.01f)
            return;

        comp.TargetHeading = diff.ToAngle();

        if (!comp.HeadingInitialized)
        {
            comp.CurrentHeading = diff.ToAngle();
            comp.HeadingInitialized = true;
        }

        // Snap current heading on the first tick before any movement has occurred
        if (comp.Stage == 0 && comp.DistanceTraveled < 0.01f)
            comp.CurrentHeading = comp.TargetHeading;
    }

    private void OnMoveInput(Entity<XenoChargerStateComponent> ent, ref MoveInputEvent args)
    {
        args.Entity.Comp.HeldMoveButtons &= ~MoveButtons.AnyDirection;
    }

    private void OnMobStateChanged(Entity<XenoChargerStateComponent> ent, ref MobStateChangedEvent args)
    {
        if (_net.IsClient || args.NewMobState == MobState.Alive)
            return;

        ResetToIdle(ent.Owner);
    }

    private void OnStartPullAttempt(Entity<XenoChargerStateComponent> ent, ref StartPullAttemptEvent args)
    {
        if (args.Puller != ent.Owner)
            return;

        args.Cancel();
    }

    private void OnPullAttempt(Entity<XenoChargerStateComponent> ent, ref PullAttemptEvent args)
    {
        if (args.PullerUid != ent.Owner)
            return;

        args.Cancelled = true;
    }

    private static Angle GetWorldRotation(Angle heading)
    {
        return heading.ToVec().ToWorldAngle();
    }
}
