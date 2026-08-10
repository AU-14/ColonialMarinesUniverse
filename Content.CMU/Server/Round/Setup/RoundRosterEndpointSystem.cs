using Content.Shared._CMU14.FactionRoster;
using Content.Shared.CMU.Round;
using Content.Shared.NPC.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU.Round;

/// <summary>
/// Configures a force-neutral personnel roster console for its committed round side.
/// </summary>
public sealed partial class RoundRosterEndpointSystem : EntitySystem
{
    private static readonly ProtoId<NpcFactionPrototype> GovforFaction = "GOVFOR";
    private static readonly ProtoId<NpcFactionPrototype> OpforFaction = "OPFOR";

    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundSetupEndpointResolvedEvent>(OnEndpointResolved);
    }

    private void OnEndpointResolved(ref RoundSetupEndpointResolvedEvent args)
    {
        if (args.Slot != RoundSetupSlot.RosterConsole)
            return;

        var roster = EnsureComp<FactionRosterConsoleComponent>(args.Endpoint);
        switch (args.Side)
        {
            case RoundSide.Govfor:
                roster.Faction = GovforFaction;
                _metadata.SetEntityName(args.Endpoint, "GOVFOR personnel console");
                _metadata.SetEntityDescription(args.Endpoint, "Lists GOVFOR personnel dossiers.");
                break;
            case RoundSide.Opfor:
                roster.Faction = OpforFaction;
                _metadata.SetEntityName(args.Endpoint, "OPFOR personnel console");
                _metadata.SetEntityDescription(args.Endpoint, "Lists OPFOR personnel dossiers.");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(args.Side), args.Side, "Unknown round side.");
        }

        if (TryComp(args.Endpoint, out RoundSetupEndpointComponent? endpoint) && endpoint.Side == null)
            _transform.SetLocalRotation(args.Endpoint, Angle.Zero);
    }
}
