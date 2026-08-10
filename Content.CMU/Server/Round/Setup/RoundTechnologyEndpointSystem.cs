using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Intel.Tech;
using Content.Shared.CMU.Round;

namespace Content.Server.CMU.Round;

/// <summary>
/// Configures a force-neutral technology console for its committed round side.
/// </summary>
public sealed partial class RoundTechnologyEndpointSystem : EntitySystem
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
        if (args.Slot != RoundSetupSlot.TechnologyTreeConsole)
            return;

        if (!TryComp(args.Endpoint, out TechControlConsoleComponent? console))
        {
            throw new InvalidOperationException(
                $"Round setup endpoint {ToPrettyString(args.Endpoint)} is a technology console without its chassis.");
        }

        _intel.SetTechnologyConsoleRoundSide((args.Endpoint, console), args.Side);
        switch (args.Side)
        {
            case RoundSide.Govfor:
                _metadata.SetEntityName(args.Endpoint, "govfor tech tree console");
                _metadata.SetEntityDescription(
                    args.Endpoint,
                    "A GovFor tech console used to make tech purchases for GovFor.");
                break;
            case RoundSide.Opfor:
                _metadata.SetEntityName(args.Endpoint, "opfor tech tree console");
                _metadata.SetEntityDescription(
                    args.Endpoint,
                    "An OpFor tech console used to make tech purchases for OpFor.");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(args.Side), args.Side, "Unknown round side.");
        }

        if (TryComp(args.Endpoint, out RoundSetupEndpointComponent? endpoint) && endpoint.Side == null)
            _transform.SetLocalRotation(args.Endpoint, Angle.Zero);
    }
}
