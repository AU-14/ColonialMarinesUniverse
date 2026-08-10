#nullable enable

using System.Collections.Immutable;
using Content.Shared.Access;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.CMU.Round;

/// <summary>
/// One mutually exclusive choice allowance in a resolved automated-vendor section.
/// </summary>
public readonly record struct RoundVendorChoice(string Id, int Amount);

/// <summary>
/// One immutable automated-vendor entry detached from prototype and runtime component data.
/// </summary>
public sealed class ResolvedRoundVendorEntry
{
    public EntProtoId Product { get; }
    public string? Name { get; }
    public int? Amount { get; }
    public int? Points { get; }
    public bool Recommended { get; }
    public LocId? GiveSquadRoleName { get; }
    public bool IsAppendSquadRoleName { get; }
    public LocId? GivePrefix { get; }
    public bool IsAppendPrefix { get; }

    internal ResolvedRoundVendorEntry(
        EntProtoId product,
        string? name,
        int? amount,
        int? points,
        bool recommended,
        LocId? giveSquadRoleName,
        bool isAppendSquadRoleName,
        LocId? givePrefix,
        bool isAppendPrefix)
    {
        Product = product;
        Name = name;
        Amount = amount;
        Points = points;
        Recommended = recommended;
        GiveSquadRoleName = giveSquadRoleName;
        IsAppendSquadRoleName = isAppendSquadRoleName;
        GivePrefix = givePrefix;
        IsAppendPrefix = isAppendPrefix;
    }
}

/// <summary>
/// One immutable, ordered section in a resolved automated-vendor profile.
/// </summary>
public sealed class ResolvedRoundVendorSection
{
    public string Name { get; }
    public RoundVendorChoice? Choice { get; }
    public string? TakeAll { get; }
    public string? TakeOne { get; }
    public int? SharedSpecLimit { get; }
    public ImmutableArray<ResolvedRoundVendorEntry> Entries { get; }

    internal ResolvedRoundVendorSection(
        string name,
        RoundVendorChoice? choice,
        string? takeAll,
        string? takeOne,
        int? sharedSpecLimit,
        ImmutableArray<ResolvedRoundVendorEntry> entries)
    {
        Name = name;
        Choice = choice;
        TakeAll = takeAll;
        TakeOne = takeOne;
        SharedSpecLimit = sharedSpecLimit;
        Entries = entries;
    }
}

/// <summary>
/// Immutable access requirements for a resolved automated-vendor profile.
/// An open profile intentionally has no access reader.
/// </summary>
public sealed class ResolvedRoundVendorAccess
{
    public bool IsOpen { get; }
    public ImmutableArray<ImmutableArray<ProtoId<AccessLevelPrototype>>> AccessLists { get; }

    internal ResolvedRoundVendorAccess(
        bool isOpen,
        ImmutableArray<ImmutableArray<ProtoId<AccessLevelPrototype>>> accessLists)
    {
        IsOpen = isOpen;
        AccessLists = accessLists;
    }
}

/// <summary>
/// Immutable automated-vendor data resolved for one force and semantic setup slot before round freeze.
/// </summary>
public sealed class ResolvedRoundVendorProfile
{
    public RoundForceId Force { get; }
    public RoundSetupSlot Slot { get; }
    public string Name { get; }
    public string Description { get; }

    /// <summary>
    /// Final base RSI selected for a presentation-aware semantic endpoint.
    /// </summary>
    public ResPath? BaseRsi { get; }
    public ResolvedRoundVendorAccess Access { get; }
    public string? PointsType { get; }
    public ImmutableArray<ProtoId<JobPrototype>> Jobs { get; }
    public ImmutableArray<ResolvedRoundVendorSection> Sections { get; }

    internal ResolvedRoundVendorProfile(
        RoundForceId force,
        RoundSetupSlot slot,
        string name,
        string description,
        ResPath? baseRsi,
        ResolvedRoundVendorAccess access,
        string? pointsType,
        ImmutableArray<ProtoId<JobPrototype>> jobs,
        ImmutableArray<ResolvedRoundVendorSection> sections)
    {
        Force = force;
        Slot = slot;
        Name = name;
        Description = description;
        BaseRsi = baseRsi;
        Access = access;
        PointsType = pointsType;
        Jobs = jobs;
        Sections = sections;
    }
}
