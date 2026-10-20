using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Examine;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Medical;

/// <summary>
///     Tells a mob what the pressure is doing to it while it's taking pressure damage: the pull of vacuum or the crush
///     of high pressure. Kept clear of <see cref="CMUSuffocationMessageSystem"/>'s can't-breathe lines, both in wording
///     and in timing, so someone stranded in space gets one message at a time instead of two stacked together.
/// </summary>
public sealed class CMUBarotraumaMessageSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private CMUSuffocationMessageSystem _suffocation = default!;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MessageCooldown = TimeSpan.FromSeconds(8);

    /// <summary>How long after a can't-breathe message a pressure message waits, so the two alternate.</summary>
    private static readonly TimeSpan SuffocationGap = TimeSpan.FromSeconds(4);

    private static readonly LocId[] LowPressureMessages =
    [
        "cmu-barotrauma-low-1",
        "cmu-barotrauma-low-2",
        "cmu-barotrauma-low-3",
        "cmu-barotrauma-low-4",
    ];

    private static readonly LocId[] HighPressureMessages =
    [
        "cmu-barotrauma-high-1",
        "cmu-barotrauma-high-2",
        "cmu-barotrauma-high-3",
    ];

    private readonly Dictionary<EntityUid, TimeSpan> _nextMessage = new();
    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        SubscribeLocalEvent<BarotraumaComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BarotraumaComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<BarotraumaComponent> ent, ref ExaminedEvent args)
    {
        if (!ent.Comp.TakingDamage || _mobState.IsDead(ent) || GetPressureKind(ent) is not { } low)
            return;

        args.PushMarkup(Loc.GetString(low ? "cmu-examine-barotrauma-low" : "cmu-examine-barotrauma-high",
            ("target", ent.Owner)), -1);
    }

    /// <summary>True for dangerously low pressure, false for dangerously high, null when the pressure is fine.</summary>
    private bool? GetPressureKind(EntityUid uid)
    {
        if (_atmosphere.GetContainingMixture(uid) is not { } mixture)
            return null;

        if (mixture.Pressure <= Atmospherics.WarningLowPressure)
            return true;

        if (mixture.Pressure >= Atmospherics.WarningHighPressure)
            return false;

        return null;
    }

    private void OnShutdown(Entity<BarotraumaComponent> ent, ref ComponentShutdown args)
    {
        _nextMessage.Remove(ent);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;

        _nextCheck = now + CheckInterval;

        var query = EntityQueryEnumerator<BarotraumaComponent>();
        while (query.MoveNext(out var uid, out var barotrauma))
        {
            if (!barotrauma.TakingDamage ||
                _mobState.IsDead(uid) ||
                _nextMessage.TryGetValue(uid, out var next) && now < next ||
                _suffocation.ShowedMessageWithin(uid, SuffocationGap))
            {
                continue;
            }

            if (GetPressureKind(uid) is not { } low)
                continue;

            var messages = low ? LowPressureMessages : HighPressureMessages;

            _nextMessage[uid] = now + MessageCooldown;
            _popup.PopupEntity(Loc.GetString(_random.Pick(messages)), uid, uid, PopupType.MediumCaution);
        }
    }
}
