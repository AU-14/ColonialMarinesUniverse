using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Atmos;

/// <summary>
/// Gives the toggle-internals action for gas tanks carried inside worn storage (satchels, pouches), the same way a
/// tank in a hand or slot gives it. Tanks held or worn directly are left to the normal item-action handling.
/// </summary>
public sealed partial class CMUStoredGasTankActionSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const int MaxNesting = 4;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(0.5);

    // Tanks whose action this system handed out, and who to.
    private readonly Dictionary<EntityUid, EntityUid> _granted = new();
    private readonly List<EntityUid> _stale = new();
    private TimeSpan _nextCheck;

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;

        _nextCheck = now + CheckInterval;

        // Forget tanks that no longer exist.
        _stale.Clear();
        foreach (var tankUid in _granted.Keys)
        {
            if (TerminatingOrDeleted(tankUid))
                _stale.Add(tankUid);
        }

        foreach (var tankUid in _stale)
        {
            _granted.Remove(tankUid);
        }

        var query = EntityQueryEnumerator<GasTankComponent>();
        while (query.MoveNext(out var uid, out var tank))
        {
            var carrier = GetStorageCarrier(uid, out var direct);

            if (carrier is { } user)
            {
                if (GetAttached(tank.ToggleActionEntity) == user)
                {
                    _granted[uid] = user;
                    continue;
                }

                _actions.AddAction(user, ref tank.ToggleActionEntity, tank.ToggleAction, uid);
                Dirty(uid, tank);
                _granted[uid] = user;
                continue;
            }

            if (!_granted.Remove(uid, out var previous))
                continue;

            // Held or worn directly by the same mob: the normal item actions own it now.
            if (direct == previous)
                continue;

            if (GetAttached(tank.ToggleActionEntity) == previous)
                _actions.RemoveAction(previous, tank.ToggleActionEntity);
        }
    }

    /// <summary>
    /// The mob with internals carrying this tank inside some storage, or null. <paramref name="direct"/> is set when
    /// the tank is held or worn directly instead.
    /// </summary>
    private EntityUid? GetStorageCarrier(EntityUid tank, out EntityUid? direct)
    {
        direct = null;
        var current = tank;
        for (var depth = 0; depth < MaxNesting; depth++)
        {
            if (!_containers.TryGetContainingContainer((current, Transform(current)), out var container))
                return null;

            if (HasComp<InternalsComponent>(container.Owner))
            {
                if (depth == 0)
                {
                    direct = container.Owner;
                    return null;
                }

                return container.Owner;
            }

            current = container.Owner;
        }

        return null;
    }

    private EntityUid? GetAttached(EntityUid? action)
    {
        return action is { } id && TryComp(id, out ActionComponent? comp) ? comp.AttachedEntity : null;
    }
}
