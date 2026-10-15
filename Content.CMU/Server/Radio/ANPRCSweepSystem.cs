using System.Linq;
using System.Numerics;
using Content.Server._RMC14.TacticalMap;
using Content.Shared.PowerCell;
using Content.Server.Radio;
using Content.Shared.CMU14.CCVar;
using Content.Shared.CMU14.Radio;
using Content.Shared._RMC14.Chat;
using Content.Shared.Radio;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Radio;

// the search receiver. an operator can walk the band hunting for somebody else's net,
// but the set can only do one job at a time: while it sweeps it will not transmit and
// it carries no traffic on the operator's own nets. a fix is built out of intercepted
// lines, each worth one hit, so a net is found by listening to it talk - not by being
// near it. once fixed, every line on that net gives the set a bearing to the speaker and
// banks a trial for breaking the net's key (ANPRCCryptoSystem.Analysis)
public sealed partial class ANPRCSweepSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ANPRCChatSystem _anprcChat = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private ANPRCFrequencyPlanSystem _freqPlan = default!;
    [Dependency] private ANPRCCryptoSystem _crypto = default!;
    [Dependency] private AU14CommsToggleSystem _comms = default!;
    [Dependency] private TacticalMapSystem _tacticalMap = default!;

    // recent emissions on each frequency, one per line. kept per line rather than one per
    // frequency: a squad shares a net, and the far man talking last must not hide the near
    // man who spoke a moment before
    private readonly Dictionary<RadioFrequency, List<BandEmission>> _band = new();

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    // lines kept per frequency, and how long. comfortably more than the busiest net puts out
    // inside the longest window anything reads
    private const int EmissionsPerFrequency = 24;
    private static readonly TimeSpan EmissionLifetime = TimeSpan.FromSeconds(60);

    // lines this close together make a net busy, and a busy net is easier to fix
    private static readonly TimeSpan TrafficWindow = TimeSpan.FromSeconds(20);

    private static readonly string[] Compass = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"];

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

        var channel = args.Channel;
        var reach = 1f;
        var encrypted = _comms.EnabledOn(channel) && !string.IsNullOrEmpty(channel.Faction);
        var bearingVisible = true;

        // a manpack sets its own power and security. a secured SC or CT set keeps its zero-DF
        // promise here too: a fixed net still gives no bearing on it (FH does leak one)
        if (TryComp(args.RadioSource, out ANPRCRadioComponent? pack))
        {
            reach = pack.TxPower.RangeMultiplier();

            var secured = encrypted &&
                          pack.Mode != RadioMode.PlainText &&
                          _crypto.HasMatchingCrypto(args.RadioSource, channel);

            encrypted = secured;
            bearingVisible = !secured || pack.Mode == RadioMode.FrequencyHopping;
        }

        RecordEmission(
            args.RadioSource,
            _freqPlan.GetFrequency(channel),
            reach,
            encrypted,
            bearingVisible,
            channel.Faction);
    }

    /// <summary>
    ///     A line on a raw frequency. Raw frequencies carry no COMSEC, so it is never encrypted; the
    ///     speaker's own manpack, if worn, sets how far it carries.
    /// </summary>
    public void RecordEmission(EntityUid source, RadioFrequency frequency)
    {
        var reach = TryComp(source, out WearingANPRCComponent? wearing) &&
                    TryComp(wearing.Radio, out ANPRCRadioComponent? pack)
            ? pack.TxPower.RangeMultiplier()
            : 1f;

        RecordEmission(source, frequency, reach, false, true, null);
    }

    private void RecordEmission(
        EntityUid source,
        RadioFrequency frequency,
        float reach,
        bool encrypted,
        bool bearingVisible,
        string? faction)
    {
        if (!_commsEnabled || frequency == RadioFrequency.Off || TerminatingOrDeleted(source))
            return;

        var xform = Transform(source);
        var now = _timing.CurTime;
        var emission = new BandEmission(source, _transform.GetWorldPosition(xform), xform.MapID, now, reach);

        if (!_band.TryGetValue(frequency, out var lines))
        {
            lines = new List<BandEmission>();
            _band[frequency] = lines;
        }

        lines.Add(emission);

        if (lines.Count > EmissionsPerFrequency)
            lines.RemoveAt(0);

        OnFixedNetEmission(frequency, emission, encrypted, bearingVisible, faction);
    }

    // a set that has fixed this frequency hears every line on it within reach, sweeping or not:
    // a bearing on the speaker, and a trial banked against the key if the line was encrypted
    private void OnFixedNetEmission(
        RadioFrequency frequency,
        BandEmission emission,
        bool encrypted,
        bool bearingVisible,
        string? faction)
    {
        var query = EntityQueryEnumerator<ANPRCRadioComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var radio, out var xform))
        {
            if (uid == emission.Source ||
                !radio.DiscoveredFrequencies.Contains(frequency) ||
                !radio.Enabled ||
                (!radio.IsEquipped && !radio.Planted))
            {
                continue;
            }

            var position = _transform.GetWorldPosition(xform);

            if (!Hears(radio, position, xform.MapID, emission))
                continue;

            if (encrypted && !string.IsNullOrEmpty(faction))
                _crypto.BankTrial(uid, faction);

            if (bearingVisible)
                ReportBearing((uid, radio), frequency, emission, position, faction);
        }
    }

    private void ReportBearing(
        Entity<ANPRCRadioComponent> ent,
        RadioFrequency frequency,
        BandEmission emission,
        Vector2 position,
        string? faction)
    {
        var radio = ent.Comp;
        var now = _timing.CurTime;

        if (radio.FixedNetDFLast.TryGetValue(frequency, out var last) && now - last < radio.FixedNetDFInterval)
            return;

        radio.FixedNetDFLast[frequency] = now;

        var offset = emission.Position - position;
        var degrees = (MathF.Atan2(offset.X, offset.Y) * 180f / MathF.PI + 360f) % 360f;
        var sector = (int) MathF.Round(degrees / 45f) % Compass.Length;

        // a rough range, not a fix: to the nearest ten metres, never closer than ten
        var distance = Math.Max(10, (int) MathF.Round(offset.Length() / 10f) * 10);

        if (TryGetOperator(ent, out var wearer))
        {
            _anprcChat.Notice(
                Loc.GetString(
                    "anprc-fixed-net-bearing",
                    ("net", GetChannelName(frequency)),
                    ("bearing", Compass[sector]),
                    ("distance", distance)),
                wearer);
        }

        // the ops map sees it too, briefly, as any other DF fix does
        if (string.IsNullOrEmpty(radio.OperatorFaction) || TerminatingOrDeleted(emission.Source))
            return;

        var viewer = radio.OperatorFaction.ToUpperInvariant();

        if (_tacticalMap.CreateFactionIntelBlip(emission.Source, (faction ?? string.Empty).ToLowerInvariant(), viewer, snapshot: true) is not { } location)
            return;

        Timer.Spawn(
            radio.FixedNetDFBlipDuration,
            () => _tacticalMap.EraseFactionIntelBlip(location.GridId, location.Key, viewer));
    }

    private static bool Hears(ANPRCRadioComponent radio, Vector2 position, MapId map, BandEmission emission)
    {
        var range = radio.SweepInterceptRange * emission.Reach;
        return emission.Map == map && (emission.Position - position).LengthSquared() <= range * range;
    }

    // the wearer, if anyone is wearing the set. a staked set has nobody to tell
    private bool TryGetOperator(Entity<ANPRCRadioComponent> ent, out EntityUid wearer)
    {
        wearer = default;

        if (!ent.Comp.IsEquipped)
            return false;

        wearer = Transform(ent.Owner).ParentUid;
        return wearer.IsValid();
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
        var cutoff = _timing.CurTime - EmissionLifetime;

        foreach (var frequency in _band.Keys.ToArray())
        {
            var lines = _band[frequency];
            lines.RemoveAll(line => line.Time < cutoff);

            if (lines.Count == 0)
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

        DecayContacts(radio, now, seconds);

        var xform = Transform(ent.Owner);
        var position = _transform.GetWorldPosition(xform);
        var map = xform.MapID;
        var cutoff = now - radio.SweepActivityWindow;

        // DWELL: the head sits on one contact. every fresh line on it counts, and counts double,
        // but the rest of the band goes unheard until the operator lets the head walk again
        if (radio.SweepDwellKilohertz >= 0)
        {
            var dwell = RadioFrequency.FromKilohertz(radio.SweepDwellKilohertz);
            radio.SweepPosition = dwell;

            if (TryGetFreshLine(radio, dwell, position, map, cutoff, out var line, out var traffic))
                RegisterHit(ent, dwell, line, traffic, radio.DwellConfidenceMultiplier);

            // fixed: nothing left to dwell for
            if (radio.DiscoveredFrequencies.Contains(dwell))
                radio.SweepDwellKilohertz = -1;
        }
        else
        {
            var start = radio.SweepPosition;
            var advance = Math.Max(1, (int) MathF.Round(radio.SweepKilohertzPerSecond * seconds));

            foreach (var frequency in _band.Keys)
            {
                if (!WasSwept(start, advance, frequency))
                    continue;

                if (TryGetFreshLine(radio, frequency, position, map, cutoff, out var line, out var traffic))
                    RegisterHit(ent, frequency, line, traffic);
            }

            radio.SweepPosition = Wrap(start.Kilohertz + advance);
        }

        Dirty(ent);

        // contacts and their confidence live only in the BUI state, so Dirty alone
        // leaves an open panel frozen. push a refresh each tick the head moves
        var ev = new ANPRCSweepUpdatedEvent();
        RaiseLocalEvent(ent.Owner, ref ev);
    }

    // the newest line on the frequency this set can hear and has not counted yet, and how busy
    // the net it heard has been around it
    private bool TryGetFreshLine(
        ANPRCRadioComponent radio,
        RadioFrequency frequency,
        Vector2 position,
        MapId map,
        TimeSpan cutoff,
        out BandEmission line,
        out int traffic)
    {
        line = default;
        traffic = 0;

        if (!_band.TryGetValue(frequency, out var lines))
            return false;

        var counted = radio.SweepContactLastCounted.GetValueOrDefault(frequency);
        var found = false;

        foreach (var candidate in lines)
        {
            if (candidate.Time < cutoff ||
                candidate.Time <= counted ||
                !Hears(radio, position, map, candidate))
            {
                continue;
            }

            if (!found || candidate.Time > line.Time)
                line = candidate;

            found = true;
        }

        if (!found)
            return false;

        foreach (var other in lines)
        {
            if (line.Time - other.Time <= TrafficWindow &&
                other.Time <= line.Time &&
                Hears(radio, position, map, other))
            {
                traffic++;
            }
        }

        return true;
    }

    private void DecayContacts(ANPRCRadioComponent radio, TimeSpan now, float seconds)
    {
        var decay = radio.SweepConfidenceDecayPerSecond * seconds;

        foreach (var frequency in radio.SweepContacts.Keys.ToArray())
        {
            // a fix already made is written down, it does not rot back out
            if (radio.DiscoveredFrequencies.Contains(frequency))
                continue;

            // a net that is still talking keeps its ground
            if (radio.SweepContactLastHit.TryGetValue(frequency, out var lastHit) &&
                now - lastHit < radio.SweepDecayGrace)
            {
                continue;
            }

            var reduced = radio.SweepContacts[frequency] - decay;

            if (reduced > 0f)
            {
                radio.SweepContacts[frequency] = reduced;
                continue;
            }

            radio.SweepContacts.Remove(frequency);
            radio.SweepContactLastHit.Remove(frequency);
            radio.SweepContactLastCounted.Remove(frequency);
        }
    }

    private void RegisterHit(
        Entity<ANPRCRadioComponent> ent,
        RadioFrequency frequency,
        BandEmission line,
        int traffic,
        float boost = 1f)
    {
        var radio = ent.Comp;

        if (radio.DiscoveredFrequencies.Contains(frequency))
            return;

        radio.SweepContactLastCounted[frequency] = line.Time;
        radio.SweepContactLastHit[frequency] = _timing.CurTime;

        TryGetOperator(ent, out var wearer);

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

        // one line is a plain hit; every other line in the traffic window adds to it, up to
        // the ceiling. spamming past that buys nothing, so the pressure is to keep traffic
        // down rather than to game it
        var multiplier = Math.Min(
            radio.SweepTrafficMultiplierMax,
            1f + (traffic - 1) * radio.SweepTrafficBonusPerEmission);

        var previous = radio.SweepContacts.GetValueOrDefault(frequency);
        var previousTier = TierOf(radio, previous);

        var confidence = previous + radio.SweepConfidencePerHit * multiplier * boost;
        var tier = TierOf(radio, confidence);

        if (tier < radio.SweepTierThresholds.Count)
        {
            radio.SweepContacts[frequency] = confidence;

            // only speak up when a digit actually falls in. reporting every catch
            // would bury the operator in identical readouts on a busy net
            if (tier > previousTier && wearer.IsValid())
            {
                _anprcChat.Notice(
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
            _anprcChat.Notice(
                Loc.GetString(
                    "anprc-sweep-resolved",
                    ("freq", TunableFrequencySystem.FormatFreq(frequency)),
                    ("net", GetChannelName(frequency))),
                wearer, ANPRCNotice.Good);
        }
    }

    public void StopSweep(Entity<ANPRCRadioComponent> ent, string? reasonLoc = null)
    {
        if (!ent.Comp.SweepEnabled)
            return;

        ent.Comp.SweepEnabled = false;
        ent.Comp.SweepLastUpdate = TimeSpan.Zero;
        ent.Comp.SweepDwellKilohertz = -1;
        Dirty(ent);

        if (reasonLoc != null && TryGetOperator(ent, out var wearer))
            _anprcChat.Notice(Loc.GetString(reasonLoc), wearer, ANPRCNotice.Warn);

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
    public static RadioFrequency MaskFrequency(RadioFrequency frequency, int tier)
    {
        if (tier >= DigitCount)
            return frequency;

        var kilohertz = frequency.Kilohertz;
        var maskedKilohertz = tier switch
        {
            <= 0 => 0,
            1 => kilohertz / 100_000 * 100_000,
            2 => kilohertz / 10_000 * 10_000,
            3 => kilohertz / 1_000 * 1_000,
            _ => kilohertz,
        };

        return RadioFrequency.FromKilohertz(maskedKilohertz);
    }

    // the masked value rendered with X in place of every unearned digit. The first
    // three tiers reveal the MHz digits; the final tier reveals the exact kHz carrier.
    public static string FormatMasked(RadioFrequency frequency, int tier)
    {
        var text = TunableFrequencySystem.FormatFreq(MaskFrequency(frequency, tier));
        if (tier >= DigitCount)
            return text;

        var earnedDigits = Math.Max(0, tier);
        var digitsSeen = 0;
        var masked = text.ToCharArray();

        for (var i = 0; i < masked.Length; i++)
        {
            if (!char.IsAsciiDigit(masked[i]))
                continue;

            digitsSeen++;
            if (digitsSeen > earnedDigits)
                masked[i] = 'X';
        }

        return new string(masked);
    }

    // digits in a band frequency, and so the length of a full ladder
    public const int DigitCount = 4;

    public string GetChannelName(RadioFrequency frequency)
    {
        return _freqPlan.TryGetChannelByFrequency(frequency, out var channel) &&
               _prototype.TryIndex(channel, out var proto)
            ? proto.LocalizedName
            : Loc.GetString("anprc-sweep-unknown-net");
    }

    private static bool WasSwept(RadioFrequency start, int advanceKilohertz, RadioFrequency candidate)
    {
        var minimum = ANPRCRadioComponent.SweepBandMin.Kilohertz;
        var maximum = ANPRCRadioComponent.SweepBandMax.Kilohertz;

        if (candidate.Kilohertz < minimum || candidate.Kilohertz > maximum)
            return false;

        var span = maximum - minimum + 1;

        if (advanceKilohertz >= span)
            return true;

        var startOffset = (start.Kilohertz - minimum) % span;
        var candidateOffset = (candidate.Kilohertz - minimum) % span;
        var distance = (candidateOffset - startOffset + span) % span;

        return distance < advanceKilohertz;
    }

    private static RadioFrequency Wrap(int kilohertz)
    {
        var minimum = ANPRCRadioComponent.SweepBandMin.Kilohertz;
        var maximum = ANPRCRadioComponent.SweepBandMax.Kilohertz;
        var span = maximum - minimum + 1;
        var offset = (kilohertz - minimum) % span;

        if (offset < 0)
            offset += span;

        return RadioFrequency.FromKilohertz(minimum + offset);
    }

    private readonly record struct BandEmission(EntityUid Source, Vector2 Position, MapId Map, TimeSpan Time, float Reach);
}

[ByRefEvent]
public record struct ANPRCSweepStoppedEvent;

// the head moved or a contact firmed up, an open panel needs the new state
[ByRefEvent]
public record struct ANPRCSweepUpdatedEvent;
