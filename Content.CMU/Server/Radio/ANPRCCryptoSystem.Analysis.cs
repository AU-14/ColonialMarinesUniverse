using System.Linq;
using Content.Shared._RMC14.Chat;
using Content.Shared.CMU14.Radio;
using Content.Shared.Database;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Radio;

// key analysis: an expert breaking an enemy faction's current key from the faceplate. the set has to
// have fixed one of that faction's nets first, every intercepted line on a fixed net banks one trial,
// and a trial only says how many positions of a guessed key are right. a break reads that faction's
// traffic on this set alone until they recrypto, and never lets the set transmit on their nets
public sealed partial class ANPRCCryptoSystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private ANPRCChatSystem _anprcChat = default!;
    [Dependency] private ANPRCFrequencyPlanSystem _freqPlan = default!;

    // the most trials a set can hold unspent. enough to finish a key in one sitting, not enough to
    // bank an afternoon of chatter and then break a fresh key the moment it is issued
    public const int DepthMax = 12;

    // the most trials the faceplate keeps listed per faction
    private const int TrialHistoryMax = 40;

    // (faction, generation) -> the key. rolled on first use, gone at round end
    private readonly Dictionary<(string Faction, int Generation), string> _keys = new();

    private void InitializeAnalysis()
    {
        Subs.BuiEvents<ANPRCCryptoSlotComponent>(ANPRCRadioUI.Key, subs =>
        {
            subs.Event<ANPRCKeyTrialMsg>(OnKeyTrial);
        });
    }

    private string GetKey(string faction)
    {
        var id = (faction, GetGeneration(faction));

        if (_keys.TryGetValue(id, out var key))
            return key;

        var symbols = ANPRCKeyAnalysis.KeySymbols.ToList();
        _random.Shuffle(symbols);
        key = new string(symbols.Take(ANPRCKeyAnalysis.KeyLength).ToArray());

        _keys[id] = key;
        return key;
    }

    /// <summary>True when this set has broken the faction's current key.</summary>
    public bool HasBrokenKey(EntityUid anprc, string? faction)
    {
        if (string.IsNullOrEmpty(faction) ||
            !TryComp(anprc, out ANPRCKeyAnalysisComponent? analysis) ||
            !analysis.Factions.TryGetValue(faction, out var work))
        {
            return false;
        }

        return work.Broken && work.Generation == GetGeneration(faction);
    }

    /// <summary>
    ///     The enemy factions this set has fixed at least one net of. Raw frequencies carry no
    ///     faction and no COMSEC, so they never count.
    /// </summary>
    public HashSet<string> GetFixedFactions(ANPRCRadioComponent radio)
    {
        var factions = new HashSet<string>();

        foreach (var frequency in radio.DiscoveredFrequencies)
        {
            if (!_freqPlan.TryGetChannelByFrequency(frequency, out var channelId) ||
                !_prototype.TryIndex(channelId, out var channel) ||
                string.IsNullOrEmpty(channel.Faction) ||
                string.Equals(channel.Faction, radio.OperatorFaction, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            factions.Add(channel.Faction);
        }

        return factions;
    }

    /// <summary>An intercepted, encrypted line on a net this set has fixed: one more trial to spend.</summary>
    public void BankTrial(EntityUid anprc, string faction)
    {
        var work = GetWork(EnsureComp<ANPRCKeyAnalysisComponent>(anprc), faction);

        if (work.Broken || work.Depth >= DepthMax)
            return;

        work.Depth++;
        RaiseLocalEvent(anprc, new ANPRCCryptoChangedEvent());
    }

    // the work against the faction's current key. a recrypto since it was started throws it away:
    // the trials were against a key nobody uses any more
    private ANPRCKeyWork GetWork(ANPRCKeyAnalysisComponent analysis, string faction)
    {
        var generation = GetGeneration(faction);

        if (!analysis.Factions.TryGetValue(faction, out var work))
        {
            work = new ANPRCKeyWork { Generation = generation };
            analysis.Factions[faction] = work;
            return work;
        }

        if (work.Generation != generation)
        {
            work.Generation = generation;
            work.Depth = 0;
            work.Trials.Clear();
            work.Broken = false;
        }

        return work;
    }

    private void OnKeyTrial(Entity<ANPRCCryptoSlotComponent> ent, ref ANPRCKeyTrialMsg args)
    {
        // breaking a key is RTO work, whatever panel view the client happens to be on
        if (!HasComp<ANPRCRadioUserComponent>(args.Actor))
        {
            _anprcChat.Notice(Loc.GetString("anprc-key-untrained"), args.Actor, ANPRCNotice.Warn);
            return;
        }

        if (!TryComp(ent.Owner, out ANPRCRadioComponent? radio) ||
            !radio.Enabled ||
            (!radio.IsEquipped && !radio.Planted))
        {
            _anprcChat.Notice(Loc.GetString("anprc-key-offline"), args.Actor, ANPRCNotice.Warn);
            return;
        }

        // only a faction whose net the set has actually fixed. the client names it, so check it
        if (!GetFixedFactions(radio).Contains(args.Faction))
        {
            _anprcChat.Notice(Loc.GetString("anprc-key-no-fix"), args.Actor, ANPRCNotice.Warn);
            return;
        }

        if (ANPRCKeyAnalysis.Normalize(args.Trial) is not { } trial)
        {
            _anprcChat.Notice(
                Loc.GetString(
                    "anprc-key-bad-trial",
                    ("length", ANPRCKeyAnalysis.KeyLength),
                    ("symbols", ANPRCKeyAnalysis.KeySymbols)),
                args.Actor, ANPRCNotice.Warn);
            return;
        }

        var analysis = EnsureComp<ANPRCKeyAnalysisComponent>(ent.Owner);
        var work = GetWork(analysis, args.Faction);

        if (work.Broken)
            return;

        if (work.Depth <= 0)
        {
            _anprcChat.Notice(Loc.GetString("anprc-key-no-depth"), args.Actor, ANPRCNotice.Warn);
            return;
        }

        work.Depth--;

        var hits = ANPRCKeyAnalysis.Hits(GetKey(args.Faction), trial);

        work.Trials.Add((trial, hits));

        if (work.Trials.Count > TrialHistoryMax)
            work.Trials.RemoveAt(0);

        if (hits == ANPRCKeyAnalysis.KeyLength)
        {
            work.Broken = true;

            _adminLogger.Add(
                LogType.Action,
                LogImpact.Medium,
                $"{ToPrettyString(args.Actor):user} broke {args.Faction} COMSEC generation {work.Generation} on {ToPrettyString(ent.Owner):radio} after {work.Trials.Count} trials");

            _anprcChat.Notice(
                Loc.GetString("anprc-key-broken", ("faction", args.Faction.ToUpperInvariant())),
                args.Actor, ANPRCNotice.Good);
        }
        else
        {
            _anprcChat.Notice(
                Loc.GetString(
                    "anprc-key-trial-result",
                    ("trial", trial),
                    ("hits", hits),
                    ("length", ANPRCKeyAnalysis.KeyLength)),
                args.Actor);
        }

        RaiseLocalEvent(ent.Owner, new ANPRCCryptoChangedEvent());
    }

    /// <summary>The faceplate's view: every fixed enemy faction, and any the set has worked on before.</summary>
    public List<ANPRCKeyAnalysisState> BuildAnalysisStates(EntityUid anprc, ANPRCRadioComponent radio)
    {
        var states = new List<ANPRCKeyAnalysisState>();
        var factions = GetFixedFactions(radio);

        TryComp(anprc, out ANPRCKeyAnalysisComponent? analysis);

        if (analysis != null)
            factions.UnionWith(analysis.Factions.Keys);

        foreach (var faction in factions.OrderBy(faction => faction))
        {
            var state = new ANPRCKeyAnalysisState
            {
                Faction = faction,
                DepthMax = DepthMax,
            };

            if (analysis != null)
            {
                var work = GetWork(analysis, faction);
                state.Depth = work.Depth;
                state.Broken = work.Broken;

                foreach (var (key, hits) in work.Trials)
                {
                    state.Trials.Add(new ANPRCKeyTrial(key, hits));
                }
            }

            states.Add(state);
        }

        return states;
    }
}
