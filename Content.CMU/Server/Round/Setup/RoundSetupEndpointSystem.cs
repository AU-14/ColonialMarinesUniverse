using System.Linq;
using Content.Server.AU14.Round;
using Content.Shared.AU14;
using Content.Shared.CMU.Round;

namespace Content.Server.CMU.Round;

/// <summary>
/// Owns semantic round setup endpoint lifecycle and announces one resolved side per generation.
/// </summary>
public sealed partial class RoundSetupEndpointSystem : EntitySystem
{
    [Dependency] private CMURoundDirectorSystem _director = default!;

    private readonly HashSet<EntityUid> _endpoints = [];
    private readonly Dictionary<EntityUid, int> _resolvedGenerations = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundSetupEndpointComponent, ComponentStartup>(OnEndpointStartup);
        SubscribeLocalEvent<RoundSetupEndpointComponent, ComponentShutdown>(OnEndpointShutdown);
        SubscribeLocalEvent<CMURoundPhaseChangedEvent>(OnRoundPhaseChanged);
    }

    private void OnEndpointStartup(Entity<RoundSetupEndpointComponent> endpoint, ref ComponentStartup args)
    {
        if (endpoint.Comp.Slot == RoundSetupSlot.None ||
            !Enum.IsDefined(typeof(RoundSetupSlot), endpoint.Comp.Slot))
        {
            throw new InvalidOperationException(
                $"Round setup endpoint {ToPrettyString(endpoint)} has an invalid semantic slot '{endpoint.Comp.Slot}'.");
        }

        _endpoints.Add(endpoint);
        if (WorldIsInitialized(_director.Phase))
            ResolveEndpoint(endpoint);
    }

    private void OnEndpointShutdown(Entity<RoundSetupEndpointComponent> endpoint, ref ComponentShutdown args)
    {
        _endpoints.Remove(endpoint);
        _resolvedGenerations.Remove(endpoint);

        var removed = new RoundSetupEndpointRemovedEvent(
            endpoint,
            _director.Generation,
            endpoint.Comp.Slot);
        RaiseLocalEvent(ref removed);
    }

    private void OnRoundPhaseChanged(ref CMURoundPhaseChangedEvent args)
    {
        if (args.Phase != CMURoundPhase.WorldInitialized)
            return;

        foreach (var uid in _endpoints.ToArray())
        {
            if (!TryComp(uid, out RoundSetupEndpointComponent? endpoint))
            {
                _endpoints.Remove(uid);
                _resolvedGenerations.Remove(uid);
                continue;
            }

            ResolveEndpoint(new Entity<RoundSetupEndpointComponent>(uid, endpoint));
        }
    }

    private void ResolveEndpoint(Entity<RoundSetupEndpointComponent> endpoint)
    {
        if (_resolvedGenerations.TryGetValue(endpoint, out var generation) && generation == _director.Generation)
            return;

        var owningSide = TryGetOwningSide(endpoint, out var resolvedOwner)
            ? (RoundSide?) resolvedOwner
            : null;
        var side = RoundSetupEndpointResolver.ResolveSide(endpoint.Comp.Side, owningSide);
        var resolved = new RoundSetupEndpointResolvedEvent(
            endpoint,
            _director.Generation,
            endpoint.Comp.Slot,
            side);
        RaiseLocalEvent(ref resolved);
        _resolvedGenerations[endpoint] = _director.Generation;
    }

    private bool TryGetOwningSide(EntityUid endpoint, out RoundSide side)
    {
        side = default;
        if (!TryComp(endpoint, out TransformComponent? transform) ||
            transform.GridUid is not { } grid ||
            !TryComp(grid, out ShipFactionComponent? shipFaction) ||
            string.IsNullOrWhiteSpace(shipFaction.Faction))
        {
            return false;
        }

        side = shipFaction.Faction switch
        {
            "govfor" => RoundSide.Govfor,
            "opfor" => RoundSide.Opfor,
            _ => throw new InvalidOperationException(
                $"Owning grid {ToPrettyString(grid)} has unsupported round side faction '{shipFaction.Faction}'."),
        };
        return true;
    }

    private static bool WorldIsInitialized(CMURoundPhase phase)
    {
        return phase is CMURoundPhase.WorldInitialized or
            CMURoundPhase.PlayersSpawned or
            CMURoundPhase.InRound;
    }
}

/// <summary>
/// Announces that a semantic round setup endpoint has a committed side for one generation.
/// </summary>
[ByRefEvent]
public readonly record struct RoundSetupEndpointResolvedEvent(
    EntityUid Endpoint,
    int Generation,
    RoundSetupSlot Slot,
    RoundSide Side);

/// <summary>
/// Announces that a tracked semantic round setup endpoint is shutting down.
/// </summary>
[ByRefEvent]
public readonly record struct RoundSetupEndpointRemovedEvent(
    EntityUid Endpoint,
    int Generation,
    RoundSetupSlot Slot);
