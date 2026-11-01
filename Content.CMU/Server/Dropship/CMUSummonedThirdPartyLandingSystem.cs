using Content.Server._RMC14.Dropship;
using Content.Server.CMU14.Ops.ThirdParty;
using Content.Server.Shuttles.Events;
using Content.Shared._RMC14.Dropship;
using Content.Shared.CMU14.Dropship;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Dropship;

/// <summary>
///     Sends a summoned third party's shuttle back to deep space a set time after it lands on its faction's ship pad.
/// </summary>
public sealed class CMUSummonedThirdPartyLandingSystem : EntitySystem
{
    private static readonly TimeSpan WarningInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

    [Dependency] private DropshipSystem _dropship = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUSummonedThirdPartyDestinationComponent, DropshipRelayedEvent<FTLCompletedEvent>>(OnLanded);
    }

    private void OnLanded(Entity<CMUSummonedThirdPartyDestinationComponent> pad, ref DropshipRelayedEvent<FTLCompletedEvent> args)
    {
        var shuttle = args.Relayer;
        if (!TryComp(pad, out DropshipDestinationComponent? destination) ||
            destination.Ship != shuttle ||
            !HasComp<CMUSummonedThirdPartyShuttleComponent>(shuttle))
        {
            return;
        }

        var departure = EnsureComp<CMUSummonedThirdPartyDepartureComponent>(shuttle);
        departure.DepartAt = _timing.CurTime + pad.Comp.Departure;
        departure.NextWarningAt = _timing.CurTime;
    }

    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUSummonedThirdPartyDepartureComponent, DropshipComponent>();
        while (query.MoveNext(out var uid, out var departure, out var dropship))
        {
            // Already gone, or moved on from the pad by hand. A shuttle that's just landed keeps its FTL component
            // through the post-flight cooldown, so only a flight that's starting or underway means it has left.
            if (dropship.Crashed ||
                TryComp(uid, out FTLComponent? flight) && flight.State is not (FTLState.Cooldown or FTLState.Available) ||
                dropship.Destination is not { } destination ||
                !HasComp<CMUSummonedThirdPartyDestinationComponent>(destination))
            {
                RemCompDeferred<CMUSummonedThirdPartyDepartureComponent>(uid);
                continue;
            }

            if (time < departure.DepartAt)
            {
                if (time >= departure.NextWarningAt)
                {
                    var seconds = (int) Math.Ceiling((departure.DepartAt - time).TotalSeconds);
                    _popup.PopupEntity(Loc.GetString("cmu-summoned-third-party-departure-warning", ("seconds", seconds)),
                        uid, PopupType.LargeCaution);
                    departure.NextWarningAt = time + WarningInterval;
                }

                continue;
            }

            if (!TryReturnToDeepSpace(uid))
                departure.DepartAt = time + RetryDelay;
        }
    }

    private bool TryReturnToDeepSpace(EntityUid shuttle)
    {
        EntityUid? returnDestination = null;
        var returns = EntityQueryEnumerator<ThirdPartyDropshipReturnDestinationComponent>();
        while (returns.MoveNext(out var uid, out var comp))
        {
            if (comp.Shuttle == shuttle)
            {
                returnDestination = uid;
                break;
            }
        }

        if (returnDestination is not { } target || TerminatingOrDeleted(target))
        {
            Log.Warning($"Summoned third party shuttle {ToPrettyString(shuttle)} has no deep space return destination.");
            return false;
        }

        var children = Transform(shuttle).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (!TryComp(child, out DropshipNavigationComputerComponent? computer))
                continue;

            _popup.PopupEntity(Loc.GetString("cmu-summoned-third-party-departing"), shuttle, PopupType.LargeCaution);
            return _dropship.FlyTo((child, computer), target, null);
        }

        Log.Warning($"Summoned third party shuttle {ToPrettyString(shuttle)} has no navigation computer to return with.");
        return false;
    }
}
