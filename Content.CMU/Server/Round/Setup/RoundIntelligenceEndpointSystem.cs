using Content.Shared._RMC14.Intel;
using Content.Shared.CMU.Round;

namespace Content.Server.CMU.Round;

/// <summary>
/// Configures a force-neutral intelligence computer for its committed round side.
/// </summary>
public sealed partial class RoundIntelligenceEndpointSystem : EntitySystem
{
    [Dependency] private IntelSystem _intel = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundSetupEndpointResolvedEvent>(OnEndpointResolved);
    }

    private void OnEndpointResolved(ref RoundSetupEndpointResolvedEvent args)
    {
        if (args.Slot != RoundSetupSlot.IntelligenceComputer)
            return;

        if (!TryComp(args.Endpoint, out IntelConsoleComponent? console))
        {
            throw new InvalidOperationException(
                $"Round setup endpoint {ToPrettyString(args.Endpoint)} is an intelligence computer without its chassis.");
        }

        _intel.SetIntelConsoleRoundSide((args.Endpoint, console), args.Side);
        switch (args.Side)
        {
            case RoundSide.Govfor:
                _metadata.SetEntityName(args.Endpoint, "intel computer");
                _metadata.SetEntityDescription(
                    args.Endpoint,
                    "An intel computer for data cataloguing and distribution.");
                break;
            case RoundSide.Opfor:
                _metadata.SetEntityName(args.Endpoint, "opfor intel computer");
                _metadata.SetEntityDescription(
                    args.Endpoint,
                    "An OpFor computer used to upload and process intelligence.");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(args.Side), args.Side, "Unknown round side.");
        }

        if (TryComp(args.Endpoint, out RoundSetupEndpointComponent? endpoint) && endpoint.Side == null)
            _transform.SetLocalRotation(args.Endpoint, Angle.Zero);
    }
}
