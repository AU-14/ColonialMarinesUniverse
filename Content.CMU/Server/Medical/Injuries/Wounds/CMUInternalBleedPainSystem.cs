using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.GameTicking;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Medical.Injuries.Wounds;

/// <summary>
/// Internal bleeds cause per-limb pain and a separate body-wide blood cough cue.
/// Multiple bleeding parts shorten the cough interval without affecting movement.
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
    [Dependency] private AudioSystem _audio = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _nextPain = new();
    private readonly Dictionary<EntityUid, TimeSpan> _nextCough = new();
    private readonly Dictionary<EntityUid, int> _bleedsByBody = new();
    private readonly List<EntityUid> _stale = new();
    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            _nextPain.Clear();
            _nextCough.Clear();
        });
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

        _bleedsByBody.Clear();
        var query = EntityQueryEnumerator<InternalBleedingComponent, BodyPartComponent>();
        while (query.MoveNext(out var part, out _, out var partComp))
        {
            if (partComp.Body is { } bleedingBody)
                _bleedsByBody[bleedingBody] = _bleedsByBody.GetValueOrDefault(bleedingBody) + 1;

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

        _stale.Clear();
        foreach (var body in _nextCough.Keys)
        {
            if (!_bleedsByBody.ContainsKey(body))
                _stale.Add(body);
        }

        foreach (var body in _stale)
            _nextCough.Remove(body);

        foreach (var (body, count) in _bleedsByBody)
        {
            if (!_nextCough.TryGetValue(body, out var next))
            {
                _nextCough[body] = now + NextCoughInterval(count);
                continue;
            }

            if (now < next || !_mobState.IsAlive(body))
                continue;

            _nextCough[body] = now + NextCoughInterval(count);
            _popup.PopupEntity(Loc.GetString("cmu-internal-bleed-cough"), body, body, PopupType.MediumCaution);
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Voice/Human/male_cough_1.ogg"), body);
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/Fluids/blood1.ogg"), body);
        }
    }

    private TimeSpan NextInterval()
    {
        return TimeSpan.FromSeconds(_random.NextDouble(MinInterval.TotalSeconds, MaxInterval.TotalSeconds));
    }

    private TimeSpan NextCoughInterval(int bleedCount)
    {
        var scale = Math.Sqrt(bleedCount);
        return TimeSpan.FromSeconds(Math.Max(8, _random.NextDouble(50, 90) / scale));
    }
}
