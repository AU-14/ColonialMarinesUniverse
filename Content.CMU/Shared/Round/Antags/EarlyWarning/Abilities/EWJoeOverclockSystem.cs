using Content.Shared.Movement.Systems;

using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Timing;
namespace Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;

/// <summary>
/// Overclock's speed, on both sides (playtest 3). Directed events still reach a component during its
/// own ComponentShutdown, so a refresh made there would re-apply the bonus it is meant to remove; the
/// handler ignores a component that is stopping, and the shutdown refresh then really drops it. The
/// same refresh runs when the overheat removes the component.
/// RB-28: an activation is capped at <see cref="EWJoeOverclockComponent.MaxDuration"/>; the server then
/// removes the component, which is how a toggle off ends it.
/// </summary>
public sealed class EWJoeOverclockSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private readonly List<EntityUid> _expired = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeOverclockComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<EWJoeOverclockComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<EWJoeOverclockComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnRefreshSpeed(Entity<EWJoeOverclockComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.LifeStage >= ComponentLifeStage.Stopping)
            return;

        args.ModifySpeed(ent.Comp.SpeedMultiplier);
    }

    private void OnStartup(Entity<EWJoeOverclockComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.EndsAt = _timing.CurTime + ent.Comp.MaxDuration;
        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // the component is server-owned state; the client only predicts its speed and sees the removal arrive
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        _expired.Clear();
        var query = EntityQueryEnumerator<EWJoeOverclockComponent>();
        while (query.MoveNext(out var uid, out var overclock))
        {
            if (overclock.LifeStage <= ComponentLifeStage.Running && now >= overclock.EndsAt)
                _expired.Add(uid);
        }

        foreach (var uid in _expired)
        {
            RemComp<EWJoeOverclockComponent>(uid);
            _popup.PopupEntity(Loc.GetString("cmu-ew-joe-xenokit-overclock-capped"), uid, uid);
        }
    }

    private void OnShutdown(Entity<EWJoeOverclockComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        // however it ends (toggle, overheat, going down, stand-down), the action stops showing active
        if (ent.Comp.Action is { } action)
        {
            _actions.SetToggled(new Entity<ActionComponent?>(action, null), false);

            // RB-28: however it ends (the cap, a toggle off, an overheat), it recharges for as long as it ran, so
            // toggling it off and straight back on cannot keep it up. Server-authoritative; the cooldown replicates.
            if (_net.IsServer)
            {
                var now = _timing.CurTime;
                var ran = now - (ent.Comp.EndsAt - ent.Comp.MaxDuration);
                if (ran > TimeSpan.Zero)
                    _actions.SetCooldown(action, now, now + (ran < ent.Comp.MaxDuration ? ran : ent.Comp.MaxDuration));
            }
        }

        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
    }
}
