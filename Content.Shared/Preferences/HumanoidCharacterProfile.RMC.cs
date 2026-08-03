using System.Linq;
using Content.Shared._RMC14.Marines.Roles.Ranks;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.NamedItems;
using Content.Shared._RMC14.Xenonids.Name;
using Content.Shared.Roles;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared.Preferences;

public sealed partial class HumanoidCharacterProfile
{
    [DataField]
    private Dictionary<ProtoId<JobPrototype>, ProtoId<RankPrototype>?> _rankPreferences = new();

    [DataField]
    public ArmorPreference ArmorPreference { get; private set; }

    public IReadOnlyDictionary<ProtoId<JobPrototype>, ProtoId<RankPrototype>?> RankPreferences => _rankPreferences;

    [DataField]
    public EntProtoId<SquadTeamComponent>? SquadPreference { get; private set; }

    [DataField]
    public SharedRMCNamedItems NamedItems { get; private set; } = new();

    [DataField]
    public bool PlaytimePerks { get; private set; } = true;

    [DataField]
    public string XenoPrefix { get; private set; } = string.Empty;

    [DataField]
    public string XenoPostfix { get; private set; } = string.Empty;

    private void CopyRmcFrom(HumanoidCharacterProfile other)
    {
        ArmorPreference = other.ArmorPreference;
        _rankPreferences = new Dictionary<ProtoId<JobPrototype>, ProtoId<RankPrototype>?>(other._rankPreferences);
        SquadPreference = other.SquadPreference;
        NamedItems = other.NamedItems;
        PlaytimePerks = other.PlaytimePerks;
        XenoPrefix = other.XenoPrefix;
        XenoPostfix = other.XenoPostfix;
        CopyCmuFrom(other);
    }

    private bool RmcMemberwiseEquals(HumanoidCharacterProfile other)
    {
        return ArmorPreference == other.ArmorPreference &&
               SquadPreference == other.SquadPreference &&
               _rankPreferences.SequenceEqual(other._rankPreferences) &&
               NamedItems == other.NamedItems &&
               PlaytimePerks == other.PlaytimePerks &&
               XenoPrefix == other.XenoPrefix &&
               XenoPostfix == other.XenoPostfix &&
               Allegiance == other.Allegiance &&
               CmuMemberwiseEquals(other);
    }

    private void AddRmcHash(ref HashCode hashCode)
    {
        hashCode.Add((int) ArmorPreference);
        hashCode.Add(_rankPreferences);
        hashCode.Add(SquadPreference);
        hashCode.Add(NamedItems);
        hashCode.Add(PlaytimePerks);
        hashCode.Add(XenoPrefix);
        hashCode.Add(XenoPostfix);
        AddCmuHash(ref hashCode);
    }

    private void EnsureRmcValid(ICommonSession session, IDependencyCollection collection, IPrototypeManager prototypeManager)
    {
        EnsureCmuValid(collection.Resolve<Robust.Shared.Configuration.IConfigurationManager>());
        ArmorPreference = ArmorPreference switch
        {
            ArmorPreference.Random => ArmorPreference.Random,
            ArmorPreference.Padded => ArmorPreference.Padded,
            ArmorPreference.Padless => ArmorPreference.Padless,
            ArmorPreference.Ridged => ArmorPreference.Ridged,
            ArmorPreference.Carrier => ArmorPreference.Carrier,
            ArmorPreference.Skull => ArmorPreference.Skull,
            ArmorPreference.Smooth => ArmorPreference.Smooth,
            _ => ArmorPreference.Random,
        };

        var ranks = RankPreferences
            .Where(pair => prototypeManager.TryIndex<JobPrototype>(pair.Key, out var job) &&
                           job.SetRankPreference &&
                           pair.Value != null &&
                           prototypeManager.HasIndex<RankPrototype>(pair.Value.Value))
            .ToDictionary();
        _rankPreferences.Clear();
        foreach (var (job, rank) in ranks)
        {
            _rankPreferences.Add(job, rank);
        }

        var componentFactory = collection.Resolve<IComponentFactory>();
        if (!prototypeManager.TryIndex(SquadPreference, out var squad) ||
            !squad.TryGetComponent(out SquadTeamComponent? team, componentFactory) ||
            !team.RoundStart)
        {
            SquadPreference = null;
        }

        static string? ValidateNamedItem(string? itemName)
        {
            return itemName?.Length > 20 ? itemName[..20] : itemName;
        }

        NamedItems = new SharedRMCNamedItems(
            ValidateNamedItem(NamedItems.PrimaryGunName),
            ValidateNamedItem(NamedItems.SidearmName),
            ValidateNamedItem(NamedItems.HelmetName),
            ValidateNamedItem(NamedItems.ArmorName),
            ValidateNamedItem(NamedItems.SentryName));

        static string ValidateXenoName(string value, bool numberEndingAllowed)
        {
            value = value.ToUpperInvariant();
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if (i > 0 && numberEndingAllowed && character is >= '0' and <= '9')
                    continue;

                if (character is < 'A' or > 'Z')
                    return string.Empty;
            }

            return value;
        }

        XenoPrefix = XenoPrefix.Trim();
        XenoPostfix = XenoPostfix.Trim();

        var xenoNames = collection.Resolve<IEntityManager>().System<SharedXenoNameSystem>();
        var prefixMax = xenoNames.GetMaxXenoPrefixLength(session);
        var postfixMax = xenoNames.GetMaxXenoPostfixLength(session);
        if (XenoPrefix.Length > prefixMax)
            XenoPrefix = XenoPrefix[..prefixMax];

        XenoPrefix = ValidateXenoName(XenoPrefix, false);
        if (XenoPrefix.Length > 2)
        {
            XenoPostfix = string.Empty;
            return;
        }

        if (XenoPostfix.Length > postfixMax)
            XenoPostfix = XenoPostfix[..postfixMax];

        XenoPostfix = ValidateXenoName(XenoPostfix, true);
    }

    public HumanoidCharacterProfile WithArmorPreference(ArmorPreference armorPreference)
    {
        return new HumanoidCharacterProfile(this) { ArmorPreference = armorPreference };
    }

    public HumanoidCharacterProfile WithRankPreference(ProtoId<JobPrototype> jobId, ProtoId<RankPrototype>? rankId)
    {
        var profile = new HumanoidCharacterProfile(this);
        if (rankId == null)
            profile._rankPreferences.Remove(jobId);
        else
            profile._rankPreferences[jobId] = rankId;
        return profile;
    }

    public HumanoidCharacterProfile WithRankPreferences(
        IReadOnlyDictionary<ProtoId<JobPrototype>, ProtoId<RankPrototype>?> rankPreferences)
    {
        var profile = new HumanoidCharacterProfile(this)
        {
            _rankPreferences = new Dictionary<ProtoId<JobPrototype>, ProtoId<RankPrototype>?>(rankPreferences),
        };
        return profile;
    }

    public HumanoidCharacterProfile WithSquadPreference(EntProtoId<SquadTeamComponent>? squadPreference)
    {
        return new HumanoidCharacterProfile(this) { SquadPreference = squadPreference };
    }

    public HumanoidCharacterProfile WithNamedItems(SharedRMCNamedItems namedItems)
    {
        return new HumanoidCharacterProfile(this) { NamedItems = namedItems };
    }

    public HumanoidCharacterProfile WithPlaytimePerks(bool playtimePerks)
    {
        return new HumanoidCharacterProfile(this) { PlaytimePerks = playtimePerks };
    }

    public HumanoidCharacterProfile WithXenoPrefix(string prefix)
    {
        return new HumanoidCharacterProfile(this) { XenoPrefix = prefix };
    }

    public HumanoidCharacterProfile WithXenoPostfix(string postfix)
    {
        return new HumanoidCharacterProfile(this) { XenoPostfix = postfix };
    }
}
