using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.GameTicking;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Medical.Injuries.Wounds;

/// <summary>
/// Someone with internal bleeding feels it: every 45-95 seconds they get "You feel a stabbing pain
/// in your left arm!" for each bleeding limb, as long as they are conscious.
/// </summary>
public sealed class CMUInternalBleedPainSystem : EntitySystem
{
    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan MaxInterval = TimeSpan.FromSeconds(95);

    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _nextPain = new();
    private readonly List<EntityUid> _stale = new();
    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _nextPain.Clear());
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;

        _nextCheck = now + CheckEvery;

        _stale.Clear();
        foreach (var part in _nextPain.Keys)
        {
            if (!HasComp<InternalBleedingComponent>(part))
                _stale.Add(part);
        }

        foreach (var part in _stale)
        {
            _nextPain.Remove(part);
        }

        var query = EntityQueryEnumerator<InternalBleedingComponent, BodyPartComponent>();
        while (query.MoveNext(out var part, out _, out var partComp))
        {
            if (!_nextPain.TryGetValue(part, out var next))
            {
                _nextPain[part] = now + NextInterval();
                continue;
            }

            if (now < next)
                continue;

            _nextPain[part] = now + NextInterval();

            if (partComp.Body is not { } body || !_mobState.IsAlive(body))
                continue;

            var message = Loc.GetString("cmu-internal-bleed-pain", ("part", CMUBodyPartNames.Describe(partComp.PartType, partComp.Symmetry)));
            _popup.PopupEntity(message, body, body, PopupType.MediumCaution);
        }
    }

    private TimeSpan NextInterval()
    {
        return TimeSpan.FromSeconds(_random.NextDouble(MinInterval.TotalSeconds, MaxInterval.TotalSeconds));
    }
}
