using System.Numerics;
using Content.Shared.CCVar;
using Content.Shared.Climbing.Components;
using Content.Shared.Climbing.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Placeable;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Climbing;

/// <summary>
///     For players who turn on <see cref="CCVars.CMUVaultOnBump"/>: walking into something vaultable starts vaulting
///     over it, like a ledge, a barricade or a railing. Tables (anything you can set things on) are left out, so
///     bumping one doesn't put you on top of it. Keep pushing into it and the vault finishes; step away and it's
///     cancelled like any other vault.
/// </summary>
/// <remarks>
///     Thin things like railings are easy to already be touching when you start pushing into them, which never
///     starts a new collision, so while you're moving this also checks what you're up against.
/// </remarks>
public sealed class CMUVaultOnBumpSystem : EntitySystem
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(0.2);

    /// <summary>How directly you have to be facing the thing to vault it, so brushing along a railing doesn't.</summary>
    private const float FacingDot = 0.5f;

    [Dependency] private ClimbSystem _climb = default!;
    [Dependency] private INetConfigurationManager _netConfig = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _nextTry = new();
    private readonly HashSet<EntityUid> _contacts = new();
    private TimeSpan _nextPoll;

    public override void Initialize()
    {
        SubscribeLocalEvent<ClimbingComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<ClimbingComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<ClimbingComponent> ent, ref ComponentShutdown args)
    {
        _nextTry.Remove(ent);
    }

    private void OnStartCollide(Entity<ClimbingComponent> ent, ref StartCollideEvent args)
    {
        if (TryComp(ent, out ActorComponent? actor) && WantsVault(ent, actor.PlayerSession))
            TryVault(ent, args.OtherEntity);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextPoll)
            return;

        _nextPoll = now + PollInterval;

        foreach (var session in _player.Sessions)
        {
            if (session.Status != SessionStatus.InGame ||
                session.AttachedEntity is not { } mob ||
                !TryComp(mob, out ClimbingComponent? climbing) ||
                !WantsVault((mob, climbing), session))
            {
                continue;
            }

            _contacts.Clear();
            _physics.GetContactingEntities(mob, _contacts);
            foreach (var other in _contacts)
            {
                if (TryVault((mob, climbing), other))
                    break;
            }
        }
    }

    private bool WantsVault(Entity<ClimbingComponent> ent, ICommonSession session)
    {
        return !ent.Comp.IsClimbing &&
               ent.Comp.DoAfter == null &&
               TryComp(ent, out InputMoverComponent? mover) &&
               mover.HasDirectionalMovement &&
               _netConfig.GetClientCVar(session.Channel, CCVars.CMUVaultOnBump);
    }

    private bool TryVault(Entity<ClimbingComponent> ent, EntityUid climbable)
    {
        if (!TryComp(climbable, out ClimbableComponent? climbableComp) ||
            !climbableComp.Vaultable ||
            HasComp<PlaceableSurfaceComponent>(climbable) ||
            !Facing(ent, climbable))
        {
            return false;
        }

        var now = _timing.CurTime;
        if (_nextTry.TryGetValue(ent, out var next) && now < next)
            return false;

        _nextTry[ent] = now + Cooldown;

        // Quietly skip things that can't be vaulted right now, rather than popping up a reason on every bump.
        if (!_climb.CanVault(climbableComp, ent, climbable, out _))
            return false;

        return _climb.TryClimb(ent, ent, climbable, out _, climbableComp, ent.Comp);
    }

    /// <summary>Whether the thing is in front of you, by where its collision box actually is.</summary>
    private bool Facing(EntityUid mob, EntityUid climbable)
    {
        var facing = _transform.GetWorldRotation(mob).ToWorldVec();
        var toTarget = _physics.GetWorldAABB(climbable).Center - _transform.GetWorldPosition(mob);
        if (toTarget.LengthSquared() < 0.0001f)
            return true;

        return Vector2.Dot(facing, Vector2.Normalize(toTarget)) >= FacingDot;
    }
}
