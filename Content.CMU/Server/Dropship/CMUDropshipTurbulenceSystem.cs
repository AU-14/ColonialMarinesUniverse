using Content.Shared._RMC14.CameraShake;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Buckle.Components;
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
///     A dropship flying between landing zones hits turbulence now and then. While it lasts, everyone
///     aboard has their screen shaken, and anyone standing who is not buckled in gets jostled about and
///     sometimes thrown to the floor. Aircrew (<see cref="AircrewSkill"/>) keep their feet.
///     Dropping out of a warship's bay starts with a lurch, a few seconds of quiet, then very heavy turbulence.
/// </summary>
public sealed class CMUDropshipTurbulenceSystem : EntitySystem
{
    private static readonly EntProtoId<SkillDefinitionComponent> AircrewSkill = "CMUSkillAircrew";

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan PulseInterval = TimeSpan.FromSeconds(1);

    // How long a bout of turbulence lasts, and the calm between bouts.
    private const float MinDuration = 2f;
    private const float MaxDuration = 10f;
    private const float MinCalm = 8f;
    private const float MaxCalm = 30f;

    // Each pulse, an unbuckled passenger is knocked down with this chance, and otherwise nudged.
    private const float KnockdownChance = 0.3f;
    private static readonly TimeSpan KnockdownTime = TimeSpan.FromSeconds(1.5);
    private const float NudgeChance = 0.5f;
    private const float NudgeDistance = 0.6f;
    private const float NudgeSpeed = 2f;

    private const int StartMessages = 7;
    private const int KnockdownMessages = 5;
    private const int LaunchMessages = 4;

    // Leaving a warship's bay: the drop, a pause, then the dropship fighting its way into the air.
    private static readonly TimeSpan LaunchPause = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan LaunchBout = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan HeavyPulseInterval = TimeSpan.FromSeconds(0.5);
    private const float HeavyKnockdownChance = 0.6f;
    private static readonly TimeSpan HeavyKnockdownTime = TimeSpan.FromSeconds(2.5);
    private const float HeavyNudgeDistance = 1.2f;

    [Dependency] private RMCCameraShakeSystem _cameraShake = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, Turbulence> _ships = new();
    private readonly List<EntityUid> _gone = new();
    private readonly List<EntityUid> _aboard = new();
    private TimeSpan _nextUpdate;

    private sealed class Turbulence
    {
        public TimeSpan NextBout;
        public TimeSpan BoutEnds;
        public TimeSpan NextPulse;
        public bool Heavy;
    }

    public override void Initialize()
    {
        SubscribeLocalEvent<DropshipLaunchedFromWarshipEvent>(OnLaunchedFromWarship);
    }

    private void OnLaunchedFromWarship(ref DropshipLaunchedFromWarshipEvent args)
    {
        var ship = args.Dropship.Owner;
        var now = _timing.CurTime;
        var start = now + LaunchPause;
        _ships[ship] = new Turbulence
        {
            BoutEnds = start + LaunchBout,
            NextBout = start + LaunchBout + Calm(),
            NextPulse = start,
            Heavy = true,
        };

        var message = Loc.GetString($"cmu-dropship-launch-drop-{_random.Next(1, LaunchMessages + 1)}");
        GetPassengers(ship);
        foreach (var mob in _aboard)
        {
            _popup.PopupEntity(message, mob, mob, PopupType.LargeCaution);
        }
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + UpdateInterval;

        _gone.Clear();
        foreach (var ship in _ships.Keys)
        {
            if (!IsInTransit(ship))
                _gone.Add(ship);
        }

        foreach (var ship in _gone)
        {
            _ships.Remove(ship);
        }

        var query = EntityQueryEnumerator<DropshipComponent, FTLComponent>();
        while (query.MoveNext(out var ship, out _, out var ftl))
        {
            if (ftl.State != FTLState.Travelling)
                continue;

            if (!_ships.TryGetValue(ship, out var turbulence))
            {
                // Some calm right after take-off before the first bout.
                _ships[ship] = turbulence = new Turbulence { NextBout = now + Calm() };
                continue;
            }

            if (now >= turbulence.BoutEnds && now >= turbulence.NextBout)
            {
                turbulence.Heavy = false;
                var duration = TimeSpan.FromSeconds(_random.NextFloat(MinDuration, MaxDuration));
                turbulence.BoutEnds = now + duration;
                turbulence.NextBout = turbulence.BoutEnds + Calm();
                turbulence.NextPulse = now;
                Announce(ship);
            }

            if (now < turbulence.BoutEnds && now >= turbulence.NextPulse)
            {
                turbulence.NextPulse = now + (turbulence.Heavy ? HeavyPulseInterval : PulseInterval);
                Pulse(ship, turbulence.Heavy);
            }
        }
    }

    private TimeSpan Calm()
    {
        return TimeSpan.FromSeconds(_random.NextFloat(MinCalm, MaxCalm));
    }

    private bool IsInTransit(EntityUid ship)
    {
        return TryComp(ship, out FTLComponent? ftl) && ftl.State == FTLState.Travelling;
    }

    private void Announce(EntityUid ship)
    {
        // Everyone aboard feels the same jolt, so they all get the same line.
        var message = Loc.GetString($"cmu-dropship-turbulence-start-{_random.Next(1, StartMessages + 1)}");
        GetPassengers(ship);
        foreach (var mob in _aboard)
        {
            _popup.PopupEntity(message, mob, mob, PopupType.LargeCaution);
        }
    }

    private void Pulse(EntityUid ship, bool heavy)
    {
        GetPassengers(ship);
        foreach (var mob in _aboard)
        {
            if (heavy)
                _cameraShake.ShakeCamera(mob, 5, _random.Next(3, 5), TimeSpan.FromSeconds(0.08));
            else
                _cameraShake.ShakeCamera(mob, 3, _random.Next(1, 3));

            if (!_mobState.IsAlive(mob) ||
                _standing.IsDown(mob) ||
                TryComp(mob, out BuckleComponent? buckle) && buckle.Buckled ||
                _skills.HasSkill(mob, AircrewSkill, 1))
            {
                continue;
            }

            if (_random.Prob(heavy ? HeavyKnockdownChance : KnockdownChance))
            {
                _stun.TryKnockdown(mob, heavy ? HeavyKnockdownTime : KnockdownTime, refresh: true);
                _popup.PopupEntity(Loc.GetString($"cmu-dropship-turbulence-knocked-down-{_random.Next(1, KnockdownMessages + 1)}"), mob, mob, PopupType.MediumCaution);
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
