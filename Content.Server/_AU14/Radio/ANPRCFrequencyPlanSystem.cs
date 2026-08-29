using System.Linq;
using System.Text;
using Content.Shared._AU14.CCVar;
using Content.Shared._AU14.Radio;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Content.Shared.Paper;
using Content.Shared.Radio;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._AU14.Radio;

// rolls a fresh signal operating plan every round: faction net frequencies are
// randomized so last round's numbers are worthless and captured frequency cards
// are worth something
public sealed partial class ANPRCFrequencyPlanSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private PaperSystem _paper = default!;

    private const int FrequencyMinKilohertz = 100_000;
    private const int FrequencyMaxKilohertz = 299_900;
    private const int FrequencyStepKilohertz = 100;

    private Dictionary<string, RadioFrequency>? _plan;
    private Dictionary<RadioFrequency, ProtoId<RadioChannelPrototype>>? _channelsByFrequency;

    private bool _commsEnabled;

    public override void Initialize()
    {
        Subs.CVar(_config, AU14CCVars.NewCommsSystem, OnCommsToggled, true);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        SubscribeLocalEvent<ANPRCFreqCardComponent, MapInitEvent>(OnFreqCardMapInit);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _plan = null;
        _channelsByFrequency = null;
    }

    // prototype reloads happen mid-round: the chem generator reloads reagent
    // prototypes on every generated chem, and admins can reload after hotfixes.
    // throwing the plan away here re-rolled every net frequency behind the backs of
    // all the printed cards, tuned slots and sweep fixes. keep the plan, reconcile
    // it against the channel list, and only reprint if something actually moved
    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (!args.WasModified<RadioChannelPrototype>())
            return;

        _channelsByFrequency = null;

        if (_plan == null)
            return;

        var changed = false;

        // channels that no longer exist or no longer qualify drop out of the plan
        foreach (var id in _plan.Keys.ToArray())
        {
            if (!_prototype.TryIndex<RadioChannelPrototype>(id, out var proto) ||
                proto.Frequency == RadioFrequency.Off ||
                string.IsNullOrEmpty(proto.Faction))
            {
                _plan.Remove(id);
                changed = true;
            }
        }

        // new channels get a frequency without disturbing the assignments everyone
        // already has written down
        var taken = new HashSet<RadioFrequency>(_plan.Values);

        foreach (var proto in _prototype.EnumeratePrototypes<RadioChannelPrototype>())
        {
            if (proto.Frequency != RadioFrequency.Off)
                taken.Add(proto.Frequency);
        }

        var added = _prototype.EnumeratePrototypes<RadioChannelPrototype>()
            .Where(proto => proto.Frequency != RadioFrequency.Off &&
                            !string.IsNullOrEmpty(proto.Faction) &&
                            !_plan.ContainsKey(proto.ID))
            .OrderBy(proto => proto.ID, StringComparer.Ordinal);

        foreach (var proto in added)
        {
            RadioFrequency frequency;

            do
            {
                var steps = (FrequencyMaxKilohertz - FrequencyMinKilohertz) / FrequencyStepKilohertz;
                var kilohertz = FrequencyMinKilohertz + _random.Next(steps + 1) * FrequencyStepKilohertz;
                frequency = RadioFrequency.FromKilohertz(kilohertz);
            }
            while (!taken.Add(frequency));

            _plan[proto.ID] = frequency;
            changed = true;
        }

        if (changed)
            ReprintCards();
    }

    private void ReprintCards()
    {
        var query = EntityQueryEnumerator<ANPRCFreqCardComponent, PaperComponent>();

        while (query.MoveNext(out var uid, out var card, out var paper))
        {
            _paper.SetContent((uid, paper), GenerateSoi(card.Faction));
        }
    }

    private void OnCommsToggled(bool enabled)
    {
        _commsEnabled = enabled;
        _channelsByFrequency = null;

        // cards printed before the toggle carry the wrong numbers, reprint them
        ReprintCards();
    }

    public RadioFrequency GetFrequency(RadioChannelPrototype channel)
    {
        if (!_commsEnabled || channel.Frequency == RadioFrequency.Off || string.IsNullOrEmpty(channel.Faction))
            return channel.Frequency;

        return GetPlan().TryGetValue(channel.ID, out var frequency)
            ? frequency
            : channel.Frequency;
    }

    // a frequency the operator already holds: an unfactioned net, or one belonging to
    // their own faction. the sweep uses this so nobody is made to grind out a number
    // that is already printed on their own freq card
    public bool IsKnownTo(RadioFrequency frequency, string? operatorFaction)
    {
        if (!TryGetChannelByFrequency(frequency, out var channel) ||
            !_prototype.TryIndex(channel, out var proto))
        {
            return false;
        }

        return string.IsNullOrEmpty(proto.Faction) ||
               string.IsNullOrEmpty(operatorFaction) ||
               string.Equals(proto.Faction, operatorFaction, StringComparison.OrdinalIgnoreCase);
    }

    public bool TryGetChannelByFrequency(RadioFrequency frequency, out ProtoId<RadioChannelPrototype> channel)
    {
        if (_channelsByFrequency == null)
        {
            _channelsByFrequency = new Dictionary<RadioFrequency, ProtoId<RadioChannelPrototype>>();

            foreach (var proto in _prototype.EnumeratePrototypes<RadioChannelPrototype>())
            {
                if (proto.ID == SharedChatSystem.HivemindChannel.Id)
                    continue;

                _channelsByFrequency.TryAdd(GetFrequency(proto), new ProtoId<RadioChannelPrototype>(proto.ID));
            }
        }

        return _channelsByFrequency.TryGetValue(frequency, out channel);
    }

    private Dictionary<string, RadioFrequency> GetPlan()
    {
        if (_plan != null)
            return _plan;

        _plan = new Dictionary<string, RadioFrequency>();

        // static channels keep their book frequencies, keep the plan clear of them
        var taken = new HashSet<RadioFrequency>();

        foreach (var proto in _prototype.EnumeratePrototypes<RadioChannelPrototype>())
        {
            if (proto.Frequency != RadioFrequency.Off)
                taken.Add(proto.Frequency);
        }

        // deterministic enumeration order so the roll count is stable
        var randomized = _prototype.EnumeratePrototypes<RadioChannelPrototype>()
            .Where(proto => proto.Frequency != RadioFrequency.Off && !string.IsNullOrEmpty(proto.Faction))
            .OrderBy(proto => proto.ID, StringComparer.Ordinal);

        foreach (var proto in randomized)
        {
            RadioFrequency frequency;

            do
            {
                var steps = (FrequencyMaxKilohertz - FrequencyMinKilohertz) / FrequencyStepKilohertz;
                var kilohertz = FrequencyMinKilohertz + _random.Next(steps + 1) * FrequencyStepKilohertz;
                frequency = RadioFrequency.FromKilohertz(kilohertz);
            }
            while (!taken.Add(frequency));

            _plan[proto.ID] = frequency;
        }

        return _plan;
    }

    private void OnFreqCardMapInit(Entity<ANPRCFreqCardComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp(ent.Owner, out PaperComponent? paper))
            return;

        _paper.SetContent((ent.Owner, paper), GenerateSoi(ent.Comp.Faction));
    }

    private string GenerateSoi(string faction)
    {
        var sb = new StringBuilder();

        sb.AppendLine("[head=2]SIGNAL OPERATING INSTRUCTIONS[/head]");
        sb.AppendLine("[head=3]AN/PRC-117G NET FREQUENCY ASSIGNMENTS[/head]");
        sb.AppendLine();

        var channels = _prototype.EnumeratePrototypes<RadioChannelPrototype>()
            .Where(proto => proto.Frequency != RadioFrequency.Off &&
                            string.Equals(proto.Faction, faction, StringComparison.OrdinalIgnoreCase))
            .OrderBy(proto => proto.LocalizedName, StringComparer.OrdinalIgnoreCase);

        foreach (var channel in channels)
        {
            sb.AppendLine($"[bold]{channel.LocalizedName}[/bold] - {TunableFrequencySystem.FormatFreq(GetFrequency(channel))} MHz");
        }

        sb.AppendLine();
        sb.AppendLine("Enter a frequency in the radio panel's FREQ tab (2592 and 259.2 both mean 259.200 MHz) to assign the matching net to a preset slot.");
        sb.AppendLine();
        sb.Append("[italic]COMSEC NOTICE: This card is a controlled document. Destroy before capture. Frequencies are assigned per operation and expire with it.[/italic]");

        return sb.ToString();
    }
}
