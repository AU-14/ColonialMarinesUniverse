using Content.Server._RMC14.Dropship;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Buckle.Components;
using Content.Shared.Camera;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Dropship;

/// <summary>
///     A dropship in flight hits turbulence now and then. While it lasts, everyone aboard but the aircrew
///     (<see cref="AircrewSkill"/>) has their view rocked, and anyone standing who is not buckled in gets
///     jostled about and sometimes thrown to the floor.
///     Dropping out of a warship's bay follows the cmss13-devs/cmss13-pve#1294 sound: the clamps let go
///     three seconds into the airlocked takeoff cue, then a few quiet seconds, then very heavy turbulence.
///     From the ground, the first bout waits until the takeoff cue's freefall is over.
/// </summary>
public sealed class CMUDropshipTurbulenceSystem : EntitySystem
{
    private static readonly EntProtoId<SkillDefinitionComponent> AircrewSkill = "CMUSkillAircrew";

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.1);

    // Timed against the cmss13-pve#1294 takeoff cues.
    private static readonly TimeSpan DeclampDelay = TimeSpan.FromSeconds(3); // into dropship_flight_airlocked_start.ogg
    private static readonly TimeSpan GroundFreefall = TimeSpan.FromSeconds(15); // dropship_flight_start.ogg

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
    [Dependency] private DropshipSystem _dropship = default!;
    [Dependency] private MobStateSystem _mobState = default!;
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
            if (!TryComp(ship, out FTLComponent? ftl) || ftl.State is not (FTLState.Starting or FTLState.Travelling))
                _gone.Add(ship);
        }

        foreach (var ship in _gone)
        {
            _flights.Remove(ship);
        }

        var query = EntityQueryEnumerator<DropshipComponent, FTLComponent>();
        while (query.MoveNext(out var ship, out _, out var ftl))
        {
            if (ftl.State is not (FTLState.Starting or FTLState.Travelling))
                continue;

            if (!_flights.TryGetValue(ship, out var flight))
                _flights[ship] = flight = Plan(ship, ftl, now);

            if (flight.LurchAt is { } lurchAt && now >= lurchAt)
            {
                flight.LurchAt = null;
                Message(ship, $"cmu-dropship-launch-drop-{_random.Next(1, LaunchMessages + 1)}", PopupType.LargeCaution);
            }

            // Ordinary turbulence only once the dropship has left the pad.
            if (ftl.State != FTLState.Travelling && now < flight.BoutStarts)
                continue;

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
        var startup = ftl.State == FTLState.Starting ? ftl.StateTime.Start : now;
        var flight = new Flight();

        if (ftl.State == FTLState.Starting && _dropship.CMUIsDepartingWarship(ship))
        {
            // A long countdown keeps the dropship clamped in the bay until just before it leaves.
            var declamp = startup + DeclampDelay;
            var leaving = ftl.StateTime.End - (DeclampDelay - TimeSpan.FromSeconds(0.5));
            if (leaving > declamp)
                declamp = leaving;

            flight.LurchAt = declamp;
            flight.Heavy = true;
            flight.Announced = true; // the lurch is the announcement
            flight.BoutStarts = declamp + LaunchPause;
            flight.BoutEnds = flight.BoutStarts + LaunchBout;
            flight.NextBout = flight.BoutEnds + Calm();
            flight.NextPulse = flight.BoutStarts;
            return flight;
        }

        flight.NextBout = startup + GroundFreefall + Calm();
        flight.BoutStarts = flight.NextBout;
        flight.BoutEnds = flight.NextBout;
        return flight;
    }

    private TimeSpan Calm()
    {
        return TimeSpan.FromSeconds(_random.NextFloat(MinCalm, MaxCalm));
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
