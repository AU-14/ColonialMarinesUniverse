using Content.Shared.CCVar;
using Content.Shared.CMU14.Traits.Quirks;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Traits.Quirks;

/// <summary>
///     Clumsy characters now and then fumble whatever they're in the middle of doing.
/// </summary>
public sealed class CMUClumsySystem : EntitySystem
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);
    private const int FumbleMessages = 4;

    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly List<Content.Shared.DoAfter.DoAfter> _active = new();

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUClumsyComponent, DoAfterComponent>();
        while (query.MoveNext(out var uid, out var clumsy, out var doAfters))
        {
            if (now < clumsy.NextCheck)
                continue;

            clumsy.NextCheck = now + CheckInterval;

            _active.Clear();
            foreach (var doAfter in doAfters.DoAfters.Values)
            {
                if (!doAfter.Cancelled && !doAfter.Completed)
                    _active.Add(doAfter);
            }

            if (_active.Count == 0 || !_random.Prob(clumsy.FumbleChancePerSecond))
                continue;

            _doAfter.Cancel(_random.Pick(_active));
            var message = Loc.GetString($"cmu-clumsy-fumble-{_random.Next(1, FumbleMessages + 1)}");
            _popup.PopupEntity(message, uid, uid, PopupType.SmallCaution);
        }
    }
}

/// <summary>
///     Marks the bodies of players who turned on <see cref="CCVars.CMUNoShakeWhenResting"/>, so others can't shake
///     them up while they're lying down on purpose. Only removes the mark it added itself.
/// </summary>
public sealed class CMUNoShakeWhenRestingPreferenceSystem : EntitySystem
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);

    [Dependency] private INetConfigurationManager _netConfig = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly HashSet<EntityUid> _marked = new();
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
                !_netConfig.GetClientCVar(session.Channel, CCVars.CMUNoShakeWhenResting))
            {
                continue;
            }

            _wanted.Add(mob);
            if (!HasComp<CMUNoShakeWhenRestingComponent>(mob))
            {
                AddComp<CMUNoShakeWhenRestingComponent>(mob);
                _marked.Add(mob);
            }
        }

        _remove.Clear();
        foreach (var uid in _marked)
        {
            if (!_wanted.Contains(uid))
                _remove.Add(uid);
        }

        foreach (var uid in _remove)
        {
            _marked.Remove(uid);
            if (Exists(uid))
                RemComp<CMUNoShakeWhenRestingComponent>(uid);
        }
    }
}
