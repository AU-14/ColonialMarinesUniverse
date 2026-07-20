using System.Linq;
using System.Numerics;
using Content.Server.PowerCell;
using Content.Server.Radio;
using Content.Shared._AU14.CCVar;
using Content.Shared._AU14.Radio;
using Content.Shared._RMC14.Chat;
using Content.Shared.Radio;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._AU14.Radio;

// the search receiver. an operator can walk the band hunting for somebody else's net,
// but the set can only do one job at a time: while it sweeps it will not transmit and
// it carries no traffic on the operator's own nets. a fix is built out of repeated
// catches on the same frequency, so it costs sustained time spent deaf and silent
// within earshot of a transmitter that keeps talking - not proximity alone
public sealed class ANPRCSweepSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedCMChatSystem _cmChat = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private ANPRCFrequencyPlanSystem _freqPlan = default!;

    // traffic seen on each frequency: where the last emission came from, when it
    // landed, and how many have stacked up since the run began. a net that keeps
    // talking builds a heavier signature than one passing the odd message
    private readonly Dictionary<int, BandEmission> _band = new();

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    // gap after which a frequency is treated as having gone quiet and its traffic
    // count starts over. matches the default activity window a sweeping set uses
    private static readonly TimeSpan TrafficWindow = TimeSpan.FromSeconds(20);

    private TimeSpan _nextUpdate;

    private bool _commsEnabled;

    public override void Initialize()
    {
        Subs.CVar(_config, AU14CCVars.NewCommsSystem, v => _commsEnabled = v, true);

        SubscribeLocalEvent<RadioSendAttemptEvent>(OnAnySend);
    }

    // every channel transmission puts energy on its frequency whether or not the
    // intended receiver was in range, so this records on the attempt, not the delivery
    private void OnAnySend(ref RadioSendAttemptEvent args)
    {
        if (!_commsEnabled)
            return;

        RecordEmission(args.RadioSource, _freqPlan.GetFrequency(args.Channel));
    }

    public void RecordEmission(EntityUid source, int frequency)
    {
        if (!_commsEnabled || frequency <= 0 || Deleted(source))
            return;

        var xform = Transform(source);
        var now = _timing.CurTime;

        // a run of traffic only counts while it stays unbroken. let the net fall
        // silent past the window and the next message starts the count from one
        var count = _band.TryGetValue(frequency, out var previous) &&
                    now - previous.Time <= TrafficWindow
            ? previous.Count + 1
            : 1;

        _band[frequency] = new BandEmission(
            _transform.GetWorldPosition(xform),
            xform.MapID,
            now,
            count);
    }

    public override void Update(float frameTime)
    {
        if (!_commsEnabled || _timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + UpdateInterval;

        PruneBand();

        var query = EntityQueryEnumerator<ANPRCRadioComponent>();

        while (query.MoveNext(out var uid, out var radio))
        {
            if (!radio.SweepEnabled)
                continue;

            UpdateSweep((uid, radio));
        }
    }

    private void PruneBand()
    {
        // the longest window any radio could care about; cheap enough to use the max
        var cutoff = _timing.CurTime - TimeSpan.FromSeconds(60);

        foreach (var frequency in _band.Keys.ToArray())
        {
            if (_band[frequency].Time < cutoff)
                _band.Remove(frequency);
        }
    }

    private void UpdateSweep(Entity<ANPRCRadioComponent> ent)
    {
        var radio = ent.Comp;

        // a set that is off, unworn or flat cannot search
        if (!radio.Enabled || (!radio.IsEquipped && !radio.Planted))
        {
            StopSweep(ent, "anprc-sweep-aborted");
            return;
        }

        if (!_powerCell.TryUseCharge(ent.Owner, radio.SweepChargeCostPerSecond))
        {
            StopSweep(ent, "anprc-sweep-aborted-power");
            return;
        }

        var now = _timing.CurTime;
        var elapsed = radio.SweepLastUpdate == TimeSpan.Zero
            ? UpdateInterval
            : now - radio.SweepLastUpdate;

        radio.SweepLastUpdate = now;

        var seconds = (float) elapsed.TotalSeconds;

        DecayContacts(radio, seconds);

        var start = radio.SweepPosition;
        var advance = Math.Max(1, (int) MathF.Round(radio.SweepStepPerSecond * seconds));

        var xform = Transform(ent.Owner);
        var position = _transform.GetWorldPosition(xform);
        var map = xform.MapID;

        var cutoff = now - radio.SweepActivityWindow;
        var rangeSquared = radio.SweepInterceptRange * radio.SweepInterceptRange;

        for (var i = 0; i < advance; i++)
        {
            var frequency = Wrap(start + i);

            if (!_band.TryGetValue(frequency, out var emission) ||
                emission.Time < cutoff ||
                emission.Map != map ||
                (emission.Position - position).LengthSquared() > rangeSquared)
            {
                continue;
            }

            RegisterHit(ent, frequency, emission.Count);
        }

        radio.SweepPosition = Wrap(start + advance);
        Dirty(ent);

        // contacts and their confidence live only in the BUI state, so Dirty alone
        // leaves an open panel frozen. push a refresh each tick the head moves
        var ev = new ANPRCSweepUpdatedEvent();
        RaiseLocalEvent(ent.Owner, ref ev);
    }

    private void DecayContacts(ANPRCRadioComponent radio, float seconds)
    {
        var decay = radio.SweepConfidenceDecayPerSecond * seconds;

        foreach (var frequency in radio.SweepContacts.Keys.ToArray())
        {
            var confidence = radio.SweepContacts[frequency];

            // a fix already made is written down, it does not rot back out
            if (radio.DiscoveredFrequencies.Contains(frequency))
                continue;

            var reduced = confidence - decay;

            if (reduced <= 0f)
                radio.SweepContacts.Remove(frequency);
            else
                radio.SweepContacts[frequency] = reduced;
        }
    }

    private void RegisterHit(Entity<ANPRCRadioComponent> ent, int frequency, int traffic)
    {
        var radio = ent.Comp;

        if (radio.DiscoveredFrequencies.Contains(frequency))
            return;

        var wearer = Transform(ent.Owner).ParentUid;

        // a net this set already holds the frequency for is not a puzzle. it surfaces
        // fully identified the moment the head crosses live traffic and costs the
        // operator nothing, so their own nets never compete with a real contact.
        // seeing your own command net light up is also the plainest possible lesson
        // that the other side can see it too
        if (_freqPlan.IsKnownTo(frequency, radio.OperatorFaction))
        {
            radio.SweepContacts[frequency] = radio.SweepResolveThreshold;
            return;
        }

        // the first message on a busy frequency is worth a plain hit, each one after
        // it sharpens the catch until the ceiling. spamming the net past that buys
        // nothing, so the pressure is to keep traffic down rather than to game it
        var multiplier = Math.Min(
            radio.SweepTrafficMultiplierMax,
            1f + (traffic - 1) * radio.SweepTrafficBonusPerEmission);

        var previous = radio.SweepContacts.GetValueOrDefault(frequency);
        var previousTier = TierOf(radio, previous);

        var confidence = previous + radio.SweepConfidencePerHit * multiplier;
        var tier = TierOf(radio, confidence);

        if (tier < radio.SweepTierThresholds.Count)
        {
            radio.SweepContacts[frequency] = confidence;

            // only speak up when a digit actually falls in. reporting every catch
            // would bury the operator in identical readouts on a busy net
            if (tier > previousTier && wearer.IsValid())
            {
                _cmChat.ChatMessageToOne(
                    Loc.GetString(
                        "anprc-sweep-contact",
                        ("freq", FormatMasked(frequency, tier))),
                    wearer);
            }

            return;
        }

        radio.SweepContacts[frequency] = radio.SweepResolveThreshold;
        radio.DiscoveredFrequencies.Add(frequency);

        if (wearer.IsValid())
        {
            _cmChat.ChatMessageToOne(
                Loc.GetString(
                    "anprc-sweep-resolved",
                    ("freq", TunableFrequencySystem.FormatFreq(frequency)),
                    ("net", GetChannelName(frequency))),
                wearer);
        }
    }

    public void StopSweep(Entity<ANPRCRadioComponent> ent, string? reasonLoc = null)
    {
        if (!ent.Comp.SweepEnabled)
            return;

        ent.Comp.SweepEnabled = false;
        ent.Comp.SweepLastUpdate = TimeSpan.Zero;
        Dirty(ent);

        var wearer = Transform(ent.Owner).ParentUid;

        if (reasonLoc != null && wearer.IsValid())
            _cmChat.ChatMessageToOne(Loc.GetString(reasonLoc), wearer);

        // the radio system owns channel granting, tell it to put the set back on the
        // operator's nets now that the receiver is free
        var ev = new ANPRCSweepStoppedEvent();
        RaiseLocalEvent(ent.Owner, ref ev);
    }

    // how many of the ladder's gates this much confidence has cleared, which is also
    // how many digits of the frequency the operator has earned
    public static int TierOf(ANPRCRadioComponent radio, float confidence)
    {
        var tier = 0;

        foreach (var threshold in radio.SweepTierThresholds)
        {
            if (confidence < threshold)
                break;

            tier++;
        }

        return tier;
    }

    // zero out the digits the operator has not earned. the masked value is what goes
    // over the wire, so the exact number is never in the BUI state early
    public static int MaskFrequency(int frequency, int tier)
    {
        return tier switch
        {
            <= 0 => 0,
            1 => frequency / 1000 * 1000,
            2 => frequency / 100 * 100,
            3 => frequency / 10 * 10,
            _ => frequency,
        };
    }

    // the masked value rendered with X in place of the unknown digits, so a partial
    // fix reads as 2.4XX rather than a misleadingly precise 2.400
    public static string FormatMasked(int frequency, int tier)
    {
        var text = TunableFrequencySystem.FormatFreq(MaskFrequency(frequency, tier));
        var unknown = Math.Max(0, DigitCount - tier);

        return unknown > 0
            ? text[..^unknown] + new string('X', unknown)
            : text;
    }

    // digits in a band frequency, and so the length of a full ladder
    public const int DigitCount = 4;

    public string GetChannelName(int frequency)
    {
        return _freqPlan.TryGetChannelByFrequency(frequency, out var channel) &&
               _prototype.TryIndex(channel, out var proto)
            ? proto.LocalizedName
            : Loc.GetString("anprc-sweep-unknown-net");
    }

    private static int Wrap(int frequency)
    {
        var span = ANPRCRadioComponent.SweepBandMax - ANPRCRadioComponent.SweepBandMin + 1;
        var offset = (frequency - ANPRCRadioComponent.SweepBandMin) % span;

        if (offset < 0)
            offset += span;

        return ANPRCRadioComponent.SweepBandMin + offset;
    }

    private readonly record struct BandEmission(Vector2 Position, MapId Map, TimeSpan Time, int Count);
}

[ByRefEvent]
public record struct ANPRCSweepStoppedEvent;

// the head moved or a contact firmed up, an open panel needs the new state
[ByRefEvent]
public record struct ANPRCSweepUpdatedEvent;
