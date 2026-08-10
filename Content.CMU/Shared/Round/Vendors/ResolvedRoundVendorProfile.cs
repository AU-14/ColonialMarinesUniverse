#nullable enable

using System.Collections.Immutable;
using Content.Shared.Access;
using Robust.Shared.Prototypes;

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
    public int? Amount { get; }

    internal ResolvedRoundVendorEntry(EntProtoId product, int? amount)
    {
        Product = product;
        Amount = amount;
    }
}

/// <summary>
/// One immutable, ordered section in a resolved automated-vendor profile.
/// </summary>
public sealed class ResolvedRoundVendorSection
{
    public string Name { get; }
    public RoundVendorChoice? Choice { get; }
    public ImmutableArray<ResolvedRoundVendorEntry> Entries { get; }

    internal ResolvedRoundVendorSection(
        string name,
        RoundVendorChoice? choice,
        ImmutableArray<ResolvedRoundVendorEntry> entries)
    {
        Name = name;
        Choice = choice;
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
    public ResolvedRoundVendorAccess Access { get; }
    public ImmutableArray<ResolvedRoundVendorSection> Sections { get; }

    internal ResolvedRoundVendorProfile(
        RoundForceId force,
        RoundSetupSlot slot,
        string name,
        string description,
        ResolvedRoundVendorAccess access,
        ImmutableArray<ResolvedRoundVendorSection> sections)
    {
        Force = force;
        Slot = slot;
        Name = name;
        Description = description;
        Access = access;
        Sections = sections;
    }
}
