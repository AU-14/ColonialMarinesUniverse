using Content.Server._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.CMU.Round;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU.Round;

/// <summary>
/// Configures force-neutral ASRS endpoints in place after their owning side is known.
/// </summary>
public sealed partial class RoundAsrsEndpointSystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private RoundAsrsConsoleCatalogSystem _catalogs = default!;
    [Dependency] private RequisitionsSystem _requisitions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundSetupEndpointResolvedEvent>(OnEndpointResolved);
    }

    private void OnEndpointResolved(ref RoundSetupEndpointResolvedEvent args)
    {
        switch (args.Slot)
        {
            case RoundSetupSlot.RequisitionsConsole:
                ResolveConsole(args.Endpoint, args.Side);
                break;
            case RoundSetupSlot.RequisitionsLift:
                ResolveLift(args.Endpoint, GetFaction(args.Side));
                break;
            default:
                return;
        }
    }

    private void ResolveConsole(EntityUid endpoint, RoundSide side)
    {
        if (!TryComp(endpoint, out RequisitionsComputerComponent? computer) ||
            !TryComp(endpoint, out AccessReaderComponent? access))
        {
            throw new InvalidOperationException(
                $"Round setup endpoint {ToPrettyString(endpoint)} is a requisitions console " +
                "without its required chassis components.");
        }

        _requisitions.SetRoundSide((endpoint, computer), side);
        _access.SetAccesses((endpoint, access), GetAccess(side));
        _catalogs.RegisterResolvedSideConsole((endpoint, computer), side);
    }

    private void ResolveLift(EntityUid endpoint, string faction)
    {
        if (!TryComp(endpoint, out RequisitionsElevatorComponent? elevator))
        {
            throw new InvalidOperationException(
                $"Round setup endpoint {ToPrettyString(endpoint)} is a requisitions lift " +
                "without its required chassis component.");
        }

        elevator.Faction = faction;
        Dirty(endpoint, elevator);
    }

    private static List<HashSet<ProtoId<AccessLevelPrototype>>> GetAccess(RoundSide side)
    {
        var (command, requisitions) = side switch
        {
            RoundSide.Govfor => (
                (ProtoId<AccessLevelPrototype>) "AU14AccessGovforCommand",
                (ProtoId<AccessLevelPrototype>) "AU14AccessGovforReq"),
            RoundSide.Opfor => (
                (ProtoId<AccessLevelPrototype>) "AU14AccessOpforCommand",
                (ProtoId<AccessLevelPrototype>) "AU14AccessOpforReq"),
            _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Unknown round side."),
        };

        return
        [
            [command],
            [requisitions],
        ];
    }

    private static string GetFaction(RoundSide side)
    {
        return side switch
        {
            RoundSide.Govfor => "govfor",
            RoundSide.Opfor => "opfor",
            _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Unknown round side."),
        };
    }
}
