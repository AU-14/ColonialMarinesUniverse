using Content.Shared.CCVar;
using Content.Shared.Climbing.Components;
using Content.Shared.Climbing.Systems;
using Content.Shared.Movement.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Climbing;

/// <summary>
///     For players who turn on <see cref="CCVars.CMUVaultOnBump"/>: walking into something vaultable starts vaulting
///     over it. Keep pushing into it and the vault finishes; step away and it's cancelled like any other vault.
/// </summary>
public sealed class CMUVaultOnBumpSystem : EntitySystem
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(1);

    [Dependency] private ClimbSystem _climb = default!;
    [Dependency] private INetConfigurationManager _netConfig = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _nextTry = new();

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
        var climbable = args.OtherEntity;
        if (ent.Comp.IsClimbing ||
            ent.Comp.DoAfter != null ||
            !TryComp(climbable, out ClimbableComponent? climbableComp) ||
            !climbableComp.Vaultable ||
            !TryComp(ent, out InputMoverComponent? mover) ||
            !mover.HasDirectionalMovement ||
            !TryComp(ent, out ActorComponent? actor) ||
            !_netConfig.GetClientCVar(actor.PlayerSession.Channel, CCVars.CMUVaultOnBump))
        {
            return;
        }

        var now = _timing.CurTime;
        if (_nextTry.TryGetValue(ent, out var next) && now < next)
            return;

        _nextTry[ent] = now + Cooldown;

        // Quietly skip things that can't be vaulted right now, rather than popping up a reason on every bump.
        if (!_climb.CanVault(climbableComp, ent, climbable, out _))
            return;

        _climb.TryClimb(ent, ent, climbable, out _, climbableComp, ent.Comp);
    }
}
