using Content.Shared._CMU14.TileMovement;
using Content.Shared.CCVar;
using Content.Shared.Movement.Components;
using Robust.Shared.Configuration;

namespace Content.Server._CMU14.TileMovement;

/// <summary>
/// Lets an admin force tile-based movement onto every player mob at once via the "cmuforcetilemovement"
/// command (backed by <see cref="CCVars.CMUTileMovementForcedForAll"/>). Applies retroactively to
/// everyone already moving around, and (via <see cref="Update"/>) to anyone who spawns/attaches to a
/// mover while it's enabled. Never touches entities that already had <see cref="CMUTileMovementComponent"/>
/// before the toggle (e.g. via the trait), so turning the toggle back off doesn't strip those.
/// </summary>
public sealed class CMUGlobalTileMovementSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private bool _forcedForAll;

    /// <summary>Entities we added CMUTileMovementComponent to ourselves, so we know what to clean up.</summary>
    private readonly HashSet<EntityUid> _forced = new();

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, CCVars.CMUTileMovementForcedForAll, OnForcedChanged, true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // ComponentStartup on InputMoverComponent is an exclusive lifecycle event already claimed by
        // ActionBlockerSystem, so newly-spawned/attached movers are picked up here instead.
        if (!_forcedForAll)
            return;

        var query = EntityQueryEnumerator<InputMoverComponent>();
        while (query.MoveNext(out var uid, out _))
            Force(uid);
    }

    private void OnForcedChanged(bool enabled)
    {
        _forcedForAll = enabled;

        if (enabled)
        {
            var query = EntityQueryEnumerator<InputMoverComponent>();
            while (query.MoveNext(out var uid, out _))
                Force(uid);

            return;
        }

        foreach (var uid in _forced)
        {
            if (Exists(uid))
                RemComp<CMUTileMovementComponent>(uid);
        }

        _forced.Clear();
    }

    private void Force(EntityUid uid)
    {
        if (HasComp<CMUTileMovementComponent>(uid))
            return;

        AddComp<CMUTileMovementComponent>(uid);
        _forced.Add(uid);
    }
}
