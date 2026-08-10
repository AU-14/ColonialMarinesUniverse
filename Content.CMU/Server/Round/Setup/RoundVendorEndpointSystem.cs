#nullable enable

using System.Linq;
using Content.Server.AU14.Round;
using Content.Server._RMC14.Vendors;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared._RMC14.Vendors;
using Content.Shared.CMU.Round;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU.Round;

/// <summary>
/// Applies director-committed force data to force-neutral automated-vendor endpoints.
/// </summary>
public sealed partial class RoundVendorEndpointSystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private CMURoundDirectorSystem _director = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private CMAutomatedVendorSystem _vendors = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundSetupEndpointResolvedEvent>(OnEndpointResolved);
    }

    private void OnEndpointResolved(ref RoundSetupEndpointResolvedEvent args)
    {
        if (args.Slot is not (RoundSetupSlot.WeaponsVendor or
            RoundSetupSlot.VehicleCrewVendor or
            RoundSetupSlot.MilitaryDoctorVendor or
            RoundSetupSlot.JuniorOfficerVendor or
            RoundSetupSlot.RadioTelephoneOperatorVendor or
            RoundSetupSlot.MilitaryPoliceVendor or
            RoundSetupSlot.SectionSergeantVendor or
            RoundSetupSlot.SquadSergeantVendor or
            RoundSetupSlot.CombatTechnicianVendor or
            RoundSetupSlot.RiflemanVendor))
            return;

        if (!TryComp(args.Endpoint, out CMAutomatedVendorComponent? vendor))
        {
            throw new InvalidOperationException(
                $"Round setup endpoint {ToPrettyString(args.Endpoint)} is a vendor without its chassis.");
        }

        if (!_director.TryGetCommittedVendorProfile(args.Side, args.Slot, out var profile))
        {
            throw new InvalidOperationException(
                $"Round setup endpoint {ToPrettyString(args.Endpoint)} has no committed {args.Side} {args.Slot} profile.");
        }

        _vendors.ApplyRoundVendorProfile((args.Endpoint, vendor), profile);
        _metadata.SetEntityName(args.Endpoint, profile.Name);
        _metadata.SetEntityDescription(args.Endpoint, profile.Description);
        ApplyAccess(args.Endpoint, profile.Access);
    }

    private void ApplyAccess(EntityUid endpoint, ResolvedRoundVendorAccess access)
    {
        if (access.IsOpen)
        {
            RemComp<AccessReaderComponent>(endpoint);
            return;
        }

        var component = EnsureComp<AccessReaderComponent>(endpoint);
        var groups = new List<HashSet<ProtoId<AccessLevelPrototype>>>(access.AccessLists.Length);
        foreach (var group in access.AccessLists)
            groups.Add(group.ToHashSet());

        _access.SetAccesses((endpoint, component), groups);
    }
}
