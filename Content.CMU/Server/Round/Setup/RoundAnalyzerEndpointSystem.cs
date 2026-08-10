using Content.Shared._CMU14.Round.Objectives.Type;
using Content.Shared.CMU.Round;

namespace Content.Server.CMU.Round;

/// <summary>
/// Configures a force-neutral analyzer endpoint for its committed round side.
/// </summary>
public sealed partial class RoundAnalyzerEndpointSystem : EntitySystem
{
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundSetupEndpointResolvedEvent>(OnEndpointResolved);
    }

    private void OnEndpointResolved(ref RoundSetupEndpointResolvedEvent args)
    {
        if (args.Slot != RoundSetupSlot.Analyzer)
            return;

        if (!TryComp(args.Endpoint, out FetchAnalyzerComponent? analyzer))
        {
            throw new InvalidOperationException(
                $"Round setup endpoint {ToPrettyString(args.Endpoint)} is an analyzer without its chassis.");
        }

        switch (args.Side)
        {
            case RoundSide.Govfor:
                analyzer.Faction = "govfor";
                _metadata.SetEntityName(args.Endpoint, "Analyzer Machine");
                break;
            case RoundSide.Opfor:
                analyzer.Faction = "opfor";
                _metadata.SetEntityName(args.Endpoint, "Analyzer Machine (Opfor)");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(args.Side), args.Side, "Unknown round side.");
        }

        _transform.SetLocalRotation(args.Endpoint, Angle.Zero);
    }
}
