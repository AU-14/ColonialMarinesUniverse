using Content.Server._RMC14.Dropship;
using Content.Shared._RMC14.Damage.ObstacleSlamming;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Buckle.Components;
using Content.Shared.Camera;
using Content.Shared.CCVar;
using Content.Shared.CMU14.WallLean;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Dropship;

/// <summary>
///     A dropship in flight hits turbulence now and then. While it lasts, everyone aboard but the aircrew
///     (<see cref="AircrewSkill"/>) has their view rocked, and anyone standing who is not buckled in or
///     bracing against a wall gets jostled about and sometimes thrown to the floor, without being hurt by it.
///     The air is only rough for the first and last half minute of a flight, climbing out and coming in to land.
///     Dropping out of a warship's bay starts with a lurch, a few quiet seconds, then very heavy turbulence.
/// </summary>
public sealed class CMUDropshipTurbulenceSystem : EntitySystem
{
    private static readonly EntProtoId<SkillDefinitionComponent> AircrewSkill = "CMUSkillAircrew";

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.1);

    // Rough air only after liftoff and before touchdown.
    private static readonly TimeSpan RoughAirWindow = TimeSpan.FromSeconds(30);
    private const float MinFirstBoutDelay = 2f;
    private const float MaxFirstBoutDelay = 6f;

    // Leaving a warship: the lurch, a quiet pause, then the dropship fighting its way into the air.
    private static readonly TimeSpan LaunchPause = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan LaunchBout = TimeSpan.FromSeconds(6);

    // How long a bout lasts, and the calm between bouts.
    private const float MinDuration = 2f;
    private const float MaxDuration = 10f;
    private const float MinCalm = 8f;
    private const float MaxCalm = 30f;

    // The view is rocked with small camera kicks, like an explosion's, every update.
    private const float KickStrength = 0.35f;
    private const float HeavyKickStrength = 1.1f;

    // Unbuckled passengers are tested once a pulse.
    private static readonly TimeSpan PulseInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan HeavyPulseInterval = TimeSpan.FromSeconds(0.5);
    private const float KnockdownChance = 0.3f;
    private const float HeavyKnockdownChance = 0.6f;
    private static readonly TimeSpan KnockdownTime = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan HeavyKnockdownTime = TimeSpan.FromSeconds(2.5);
    private const float NudgeChance = 0.5f;
    private const float NudgeDistance = 0.6f;
    private const float HeavyNudgeDistance = 1.2f;
    private const float NudgeSpeed = 2f;

    // Someone only gets one turbulence message every so often, so they can be read.
    private static readonly TimeSpan MessageCooldown = TimeSpan.FromSeconds(4);

    private const int StartMessages = 7;
    private const int KnockdownMessages = 5;
    private const int LaunchMessages = 4;

    [Dependency] private SharedCameraRecoilSystem _cameraRecoil = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private DropshipSystem _dropship = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private RMCObstacleSlammingSystem _obstacleSlamming = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, Flight> _flights = new();
    private readonly Dictionary<EntityUid, TimeSpan> _nextMessage = new();
    private readonly List<EntityUid> _gone = new();
    private readonly List<EntityUid> _aboard = new();
    private TimeSpan _nextUpdate;

    private sealed class Flight
    {
        public TimeSpan? LurchAt;
        public TimeSpan NextBout;
        public TimeSpan BoutStarts;
        public TimeSpan BoutEnds;
        public TimeSpan NextPulse;
        public bool Heavy;
        public bool Announced;
        public bool Landing;
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + UpdateInterval;

        _gone.Clear();
        foreach (var ship in _flights.Keys)
        {
            if (!TryComp(ship, out FTLComponent? ftl) || !Flying(ftl))
                _gone.Add(ship);
        }

        foreach (var ship in _gone)
        {
            _flights.Remove(ship);
        }

        var query = EntityQueryEnumerator<DropshipComponent, FTLComponent>();
        while (query.MoveNext(out var ship, out _, out var ftl))
        {
            if (!Flying(ftl))
                continue;

            if (!_flights.TryGetValue(ship, out var flight))
                _flights[ship] = flight = Plan(ship, ftl, now);

            if (flight.LurchAt is { } lurchAt && now >= lurchAt)
            {
                flight.LurchAt = null;
                Message(ship, $"cmu-dropship-launch-drop-{_random.Next(1, LaunchMessages + 1)}", PopupType.LargeCaution);
            }

            // Ordinary turbulence only once the dropship has left the pad.
            if (ftl.State == FTLState.Starting && now < flight.BoutStarts)
                continue;

            var untilTouchdown = UntilTouchdown(ftl, now);
            var sinceLiftoff = ftl.State == FTLState.Travelling ? now - ftl.StateTime.Start : TimeSpan.Zero;
            var roughAir = ftl.State == FTLState.Starting ||
                           sinceLiftoff <= RoughAirWindow ||
                           untilTouchdown <= RoughAirWindow;

            // Smooth air in the middle of the flight: a bout still going ends here.
            if (!roughAir)
            {
                if (now < flight.BoutEnds)
                    flight.BoutEnds = now;

                continue;
            }

            // Coming in to land, the next bout comes soon rather than after a long calm.
            if (!flight.Landing && untilTouchdown <= RoughAirWindow)
            {
                flight.Landing = true;
                var soon = now + FirstBoutDelay();
                if (flight.NextBout > soon)
                    flight.NextBout = soon;
            }

            if (now >= flight.BoutEnds && now >= flight.NextBout)
            {
                var duration = TimeSpan.FromSeconds(_random.NextFloat(MinDuration, MaxDuration));
                flight.Heavy = false;
                flight.Announced = false;
                flight.BoutStarts = now;
                flight.BoutEnds = now + duration;
                flight.NextBout = flight.BoutEnds + Calm();
                flight.NextPulse = now;
            }

            if (now < flight.BoutStarts || now >= flight.BoutEnds)
                continue;

            if (!flight.Announced)
            {
                flight.Announced = true;
                Message(ship, $"cmu-dropship-turbulence-start-{_random.Next(1, StartMessages + 1)}", PopupType.LargeCaution);
            }

            Shake(ship, flight.Heavy);

            if (now >= flight.NextPulse)
            {
                flight.NextPulse = now + (flight.Heavy ? HeavyPulseInterval : PulseInterval);
                Throw(ship, flight.Heavy);
            }
        }
    }

    /// <summary>
    ///     Lays out the start of a flight the moment the dropship begins its takeoff.
    /// </summary>
    private Flight Plan(EntityUid ship, FTLComponent ftl, TimeSpan now)
    {
        var liftoff = ftl.State == FTLState.Starting ? ftl.StateTime.End : now;
        var flight = new Flight();

        if (ftl.State == FTLState.Starting && _dropship.CMUIsDepartingWarship(ship))
        {
            // The dropship drops out of the bay the moment it leaves.
            var declamp = liftoff;
            flight.LurchAt = declamp;
            flight.Heavy = true;
            flight.Announced = true; // the lurch is the announcement
            flight.BoutStarts = declamp + LaunchPause;
            flight.BoutEnds = flight.BoutStarts + LaunchBout;
            flight.NextBout = flight.BoutEnds + Calm();
            flight.NextPulse = flight.BoutStarts;
            return flight;
        }

        flight.NextBout = liftoff + FirstBoutDelay();
        flight.BoutStarts = flight.NextBout;
        flight.BoutEnds = flight.NextBout;
        return flight;
    }

    private TimeSpan Calm()
    {
        return TimeSpan.FromSeconds(_random.NextFloat(MinCalm, MaxCalm));
    }

    private TimeSpan FirstBoutDelay()
    {
        return TimeSpan.FromSeconds(_random.NextFloat(MinFirstBoutDelay, MaxFirstBoutDelay));
    }

    private static bool Flying(FTLComponent ftl)
    {
        return ftl.State is FTLState.Starting or FTLState.Travelling or FTLState.Arriving;
    }

    /// <summary>
    ///     How long until the dropship touches down. Travel ends when the final approach starts, which takes the
    ///     standard shuttle arrival time.
    /// </summary>
    private TimeSpan UntilTouchdown(FTLComponent ftl, TimeSpan now)
    {
        return ftl.State switch
        {
            FTLState.Arriving => ftl.StateTime.End - now,
            FTLState.Travelling => ftl.StateTime.End - now + TimeSpan.FromSeconds(_config.GetCVar(CCVars.FTLArrivalTime)),
            _ => TimeSpan.MaxValue,
        };
    }

    private bool IsAircrew(EntityUid mob)
    {
        return _skills.HasSkill(mob, AircrewSkill, 1);
    }

    private void Shake(EntityUid ship, bool heavy)
    {
        var strength = heavy ? HeavyKickStrength : KickStrength;
        GetPassengers(ship);
        foreach (var mob in _aboard)
        {
            if (IsAircrew(mob))
                continue;

            var kick = _random.NextAngle().ToVec() * strength * _random.NextFloat(0.5f, 1f);
            _cameraRecoil.KickCamera(mob, kick);
        }
    }

    private void Throw(EntityUid ship, bool heavy)
    {
        GetPassengers(ship);
        foreach (var mob in _aboard)
        {
            if (!_mobState.IsAlive(mob) ||
                _standing.IsDown(mob) ||
                TryComp(mob, out BuckleComponent? buckle) && buckle.Buckled ||
                HasComp<CMUWallLeaningComponent>(mob) ||
                IsAircrew(mob))
            {
                continue;
            }

            if (_random.Prob(heavy ? HeavyKnockdownChance : KnockdownChance))
            {
                _stun.TryKnockdown(mob, heavy ? HeavyKnockdownTime : KnockdownTime, refresh: true);
                TryMessage(mob, Loc.GetString($"cmu-dropship-turbulence-knocked-down-{_random.Next(1, KnockdownMessages + 1)}"), PopupType.MediumCaution);
                Nudge(mob, heavy);
            }
            else if (heavy || _random.Prob(NudgeChance))
            {
                Nudge(mob, heavy);
            }
        }
    }

    private void Nudge(EntityUid mob, bool heavy)
    {
        // Being thrown about the cabin doesn't hurt.
        _obstacleSlamming.MakeImmune(mob);

        var direction = _random.NextAngle().ToVec() * (heavy ? HeavyNudgeDistance : NudgeDistance);
        _throwing.TryThrow(mob, direction, NudgeSpeed, animated: false, playSound: false, doSpin: false);
    }

    /// <summary>
    ///     Shows everyone aboard the same line. These always show, and hold off other messages for a while.
    /// </summary>
    private void Message(EntityUid ship, string locId, PopupType type)
    {
        var message = Loc.GetString(locId);
        var until = _timing.CurTime + MessageCooldown;
        GetPassengers(ship);
        foreach (var mob in _aboard)
        {
            _nextMessage[mob] = until;
            _popup.PopupEntity(message, mob, mob, type);
        }
    }

    /// <summary>
    ///     Shows one person a line unless they have just had one.
    /// </summary>
    private void TryMessage(EntityUid mob, string message, PopupType type)
    {
        var now = _timing.CurTime;
        if (_nextMessage.TryGetValue(mob, out var next) && now < next)
            return;

        _nextMessage[mob] = now + MessageCooldown;
        _popup.PopupEntity(message, mob, mob, type);
    }

    private void GetPassengers(EntityUid ship)
    {
        _aboard.Clear();
        var mobs = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (mobs.MoveNext(out var mob, out _, out var xform))
        {
            if (xform.GridUid == ship)
                _aboard.Add(mob);
        }
    }
}
