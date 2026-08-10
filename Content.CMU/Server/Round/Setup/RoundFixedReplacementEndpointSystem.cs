using Content.Shared.CMU.Round;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU.Round;

/// <summary>
/// Replaces semantic setup markers with side-specific fixed assets after the round plan is committed.
/// </summary>
public sealed partial class RoundFixedReplacementEndpointSystem : EntitySystem
{
    private static readonly EntProtoId GovforCallsignLaptop = "AU14ItemLaptopCallsignGOVFOR";
    private static readonly EntProtoId OpforCallsignLaptop = "AU14ItemLaptopCallsignOPFOR";
    private static readonly EntProtoId GovforShipCommunicationsArray = "AU14CommsArrayShipGovfor";
    private static readonly EntProtoId OpforShipCommunicationsArray = "AU14CommsArrayShipOpfor";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundSetupEndpointResolvedEvent>(OnEndpointResolved);
    }

    private void OnEndpointResolved(ref RoundSetupEndpointResolvedEvent args)
    {
        var replacement = (args.Slot, args.Side) switch
        {
            (RoundSetupSlot.CallsignLaptop, RoundSide.Govfor) => GovforCallsignLaptop,
            (RoundSetupSlot.CallsignLaptop, RoundSide.Opfor) => OpforCallsignLaptop,
            (RoundSetupSlot.ShipCommunicationsArray, RoundSide.Govfor) => GovforShipCommunicationsArray,
            (RoundSetupSlot.ShipCommunicationsArray, RoundSide.Opfor) => OpforShipCommunicationsArray,
            _ => (EntProtoId?) null,
        };
        if (replacement == null)
            return;

        if (!TryComp(args.Endpoint, out TransformComponent? transform))
        {
            throw new InvalidOperationException(
                $"Round setup endpoint {ToPrettyString(args.Endpoint)} has no transform for fixed replacement.");
        }

        SpawnAttachedTo(
            replacement.Value,
            transform.Coordinates,
            rotation: transform.LocalRotation);
        QueueDel(args.Endpoint);
    }
}
