using Content.Shared.CCVar;
using Content.Shared.CMU14.TileMovement;
using Content.Shared.Movement.Components;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.TileMovement;

/// <summary>
/// Gives tile movement to whatever a player controls when they have turned it on in their options
/// (<see cref="CCVars.CMUTileMovementPreferred"/>), and takes it away when they turn it off or leave
/// that body. Only removes tile movement it added itself, so the admin force-all toggle is untouched.
/// </summary>
public sealed class CMUTileMovementPreferenceSystem : EntitySystem
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(0.5);

    [Dependency] private INetConfigurationManager _netConfig = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;

    /// <summary>Entities we gave tile movement to, so we know what to take back.</summary>
    private readonly HashSet<EntityUid> _given = new();
    private readonly HashSet<EntityUid> _wanted = new();
    private readonly List<EntityUid> _remove = new();
    private TimeSpan _nextCheck;

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextCheck)
            return;

        _nextCheck = _timing.CurTime + CheckInterval;

        _wanted.Clear();
        foreach (var session in _player.Sessions)
        {
            if (session.Status != SessionStatus.InGame ||
                session.AttachedEntity is not { } mob ||
                !HasComp<InputMoverComponent>(mob) ||
                !_netConfig.GetClientCVar(session.Channel, CCVars.CMUTileMovementPreferred))
            {
                continue;
            }

            _wanted.Add(mob);
            if (!HasComp<CMUTileMovementComponent>(mob))
            {
                AddComp<CMUTileMovementComponent>(mob);
                _given.Add(mob);
            }
        }

        _remove.Clear();
        foreach (var uid in _given)
        {
            if (!_wanted.Contains(uid))
                _remove.Add(uid);
        }

        foreach (var uid in _remove)
        {
            _given.Remove(uid);
            if (Exists(uid))
                RemComp<CMUTileMovementComponent>(uid);
        }
    }
}
