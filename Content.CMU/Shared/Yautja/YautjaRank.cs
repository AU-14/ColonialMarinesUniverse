using Content.Shared.Access;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Yautja;

[Serializable, NetSerializable]
public enum YautjaRank : byte
{
    Unblooded,
    YoungBlood,
    Blooded,
    Elite,
    Elder,
    Leader,
    Ancient,
}

public sealed record YautjaRankInfo(
    LocId LocalizedName,
    string IconState,
    ProtoId<AccessLevelPrototype>[] AccessTags,
    bool UniqueSetsAllowed,
    bool BypassesPredatorSlotCap);

[Serializable, NetSerializable]
public enum YautjaProfileStatus : byte
{
    Normal,
    Council,
    Leader,
}

[Serializable, NetSerializable]
public sealed class YautjaProfileCapabilities
{
    public YautjaProfileCapabilities(
        YautjaRank rank,
        bool canUseUnique,
        bool canUseAncientSets,
        bool canUseCouncilStatus = false,
        bool canUseLeaderStatus = false)
    {
        Rank = rank;
        CanUseUnique = canUseUnique;
        CanUseAncientSets = canUseAncientSets;
        CanUseCouncilStatus = canUseCouncilStatus;
        CanUseLeaderStatus = canUseLeaderStatus;
    }

    public YautjaRank Rank { get; }
    public bool CanUseUnique { get; }
    public bool CanUseAncientSets { get; }
    public bool CanUseCouncilStatus { get; }
    public bool CanUseLeaderStatus { get; }

    public bool CanUseStatus(YautjaProfileStatus status)
    {
        return status switch
        {
            YautjaProfileStatus.Normal => true,
            YautjaProfileStatus.Council => CanUseCouncilStatus,
            YautjaProfileStatus.Leader => CanUseLeaderStatus,
            _ => false,
        };
    }

    public YautjaProfileStatus SanitizeStatus(YautjaProfileStatus status)
    {
        if (CanUseStatus(status))
            return status;

        if (CanUseCouncilStatus)
            return YautjaProfileStatus.Council;

        if (CanUseLeaderStatus)
            return YautjaProfileStatus.Leader;

        return YautjaProfileStatus.Normal;
    }

    public YautjaRank ResolveRank(YautjaProfileStatus status)
    {
        return status switch
        {
            YautjaProfileStatus.Normal when CanUseCouncilStatus || CanUseLeaderStatus => YautjaRank.Blooded,
            // an Ancient who also holds the leader flag can still play as just a clan leader
            YautjaProfileStatus.Leader when CanUseLeaderStatus => YautjaRank.Leader,
            _ => Rank,
        };
    }

    public YautjaProfileCapabilities ForStatus(YautjaProfileStatus status)
    {
        var sanitizedStatus = SanitizeStatus(status);
        var rank = ResolveRank(sanitizedStatus);
        return new(
            rank,
            YautjaRankResolver.CanUseUnique(rank),
            CanUseAncientSets,
            CanUseCouncilStatus,
            CanUseLeaderStatus);
    }

    public bool CanUseCape(YautjaCapeStyle style)
    {
        return Enum.IsDefined(style) &&
               (style is not (YautjaCapeStyle.Ceremonial or YautjaCapeStyle.BoneCeremonial) ||
                Rank is YautjaRank.Leader or YautjaRank.Ancient);
    }

    // Ancient sets need the Council / Council Legacy whitelist, Leader sets need Clan Leader or Ancient rank.
    // elites and elders get none, that's a CMU rule (cmss13 opens elite sets from Elite)
    public bool CanUseSetLock(YautjaSetLock setLock)
    {
        return setLock switch
        {
            YautjaSetLock.Ancient => CanUseAncientSets,
            YautjaSetLock.ClanLeader => CanUseUnique,
            _ => false,
        };
    }

    public bool CanUseUniqueSet(YautjaUniqueSet unique)
    {
        return Enum.IsDefined(unique) &&
               (unique == YautjaUniqueSet.None || CanUseSetLock(YautjaCharacterProfile.GetSetLock(unique)));
    }

    // Every bracer skin is open to everyone.
    public bool CanUseBracer(YautjaBracerMaterial material)
    {
        return Enum.IsDefined(material);
    }

    public bool CanUseLegacySet(YautjaLegacySet legacy)
    {
        return Enum.IsDefined(legacy) &&
               (legacy == YautjaLegacySet.None || CanUseSetLock(YautjaCharacterProfile.GetSetLock(legacy)));
    }

    public static YautjaProfileCapabilities Default =>
        new(YautjaRank.Blooded, false, false);
}

public static class YautjaRankMetadata
{
    private static readonly ProtoId<AccessLevelPrototype>[] SecureAccess =
    [
        "CMUAccessYautjaSecure",
    ];

    private static readonly ProtoId<AccessLevelPrototype>[] EliteAccess =
    [
        "CMUAccessYautjaSecure",
        "CMUAccessYautjaElite",
    ];

    private static readonly ProtoId<AccessLevelPrototype>[] ElderAccess =
    [
        "CMUAccessYautjaSecure",
        "CMUAccessYautjaElite",
        "CMUAccessYautjaElder",
    ];

    private static readonly ProtoId<AccessLevelPrototype>[] LeaderAccess =
    [
        "CMUAccessYautjaSecure",
        "CMUAccessYautjaElite",
        "CMUAccessYautjaElder",
        "CMUAccessYautjaLeader",
    ];

    private static readonly ProtoId<AccessLevelPrototype>[] AncientAccess =
    [
        "CMUAccessYautjaSecure",
        "CMUAccessYautjaElite",
        "CMUAccessYautjaElder",
        "CMUAccessYautjaLeader",
        "CMUAccessYautjaAncient",
    ];

    public static readonly YautjaRank[] Order =
    [
        YautjaRank.Unblooded,
        YautjaRank.YoungBlood,
        YautjaRank.Blooded,
        YautjaRank.Elite,
        YautjaRank.Elder,
        YautjaRank.Leader,
        YautjaRank.Ancient,
    ];

    public static YautjaRankInfo For(YautjaRank rank)
    {
        return rank switch
        {
            YautjaRank.Unblooded => new YautjaRankInfo("cmu-yautja-rank-unblooded", "predhud", SecureAccess, false, false),
            YautjaRank.YoungBlood => new YautjaRankInfo("cmu-yautja-rank-youngblood", "predhud", SecureAccess, false, false),
            YautjaRank.Blooded => new YautjaRankInfo("cmu-yautja-rank-blooded", "predhud", SecureAccess, false, false),
            YautjaRank.Elite => new YautjaRankInfo("cmu-yautja-rank-elite", "predhud", EliteAccess, false, false),
            YautjaRank.Elder => new YautjaRankInfo("cmu-yautja-rank-elder", "predhud", ElderAccess, false, false),
            YautjaRank.Leader => new YautjaRankInfo("cmu-yautja-rank-leader", "leaderhud", LeaderAccess, true, true),
            YautjaRank.Ancient => new YautjaRankInfo("cmu-yautja-rank-ancient", "councilhud", AncientAccess, true, true),
            _ => new YautjaRankInfo("cmu-yautja-rank-blooded", "predhud", SecureAccess, false, false),
        };
    }

    public static ProtoId<AccessLevelPrototype>[] GetAccessTags(YautjaRank rank)
    {
        return For(rank).AccessTags;
    }

    public static ProtoId<AccessLevelPrototype>[] GetRackAccessTags(YautjaRank rank)
    {
        return rank switch
        {
            YautjaRank.Elder => ["CMUAccessYautjaElder", "CMUAccessYautjaAncient"],
            _ => ["CMUAccessYautjaSecure"],
        };
    }
}

public static class YautjaRankResolver
{
    public static YautjaRank ResolveForHunter(YautjaCharacterProfile? profile)
    {
        if (profile == null)
            return YautjaRank.Blooded;

        if (profile.ClanRank is { } clanRank && Enum.IsDefined(clanRank))
            return clanRank;

        return FromOwnerRank(profile.OwnerRank);
    }

    public static YautjaRank FromOwnerRank(YautjaBracerOwnerRank ownerRank)
    {
        return ownerRank switch
        {
            YautjaBracerOwnerRank.Elite => YautjaRank.Elite,
            YautjaBracerOwnerRank.Elder => YautjaRank.Elder,
            YautjaBracerOwnerRank.Leader => YautjaRank.Leader,
            YautjaBracerOwnerRank.Admin => YautjaRank.Ancient,
            _ => YautjaRank.Blooded,
        };
    }

    public static YautjaBracerOwnerRank ToOwnerRank(YautjaRank rank)
    {
        return rank switch
        {
            YautjaRank.Elite => YautjaBracerOwnerRank.Elite,
            YautjaRank.Elder => YautjaBracerOwnerRank.Elder,
            YautjaRank.Leader => YautjaBracerOwnerRank.Leader,
            YautjaRank.Ancient => YautjaBracerOwnerRank.Admin,
            _ => YautjaBracerOwnerRank.Unblooded,
        };
    }

    public static bool CanUseUnique(YautjaRank rank)
    {
        return YautjaRankMetadata.For(rank).UniqueSetsAllowed;
    }

    public static bool CanUseUnique(YautjaCharacterProfile? profile)
    {
        return CanUseUnique(ResolveForHunter(profile));
    }
}
