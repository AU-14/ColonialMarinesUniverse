using Content.Shared.Buckle.Components;
using Content.Shared.Follower.Components;
using Content.Shared.Ghost.Components;

namespace Content.Shared.CMU14.Fighter;

/// <summary>Resolves a crew member or spectator to a presentation seat, without granting controls.</summary>
public sealed class FighterViewSystem : EntitySystem
{
    public bool TryGetView(EntityUid? viewer, out Entity<FighterSeatComponent> seat, out bool spectator)
    {
        seat = default;
        spectator = true;
        if (viewer is not { } user || TerminatingOrDeleted(user)) return false;
        if (TryCrewSeat(user, out seat))
        {
            spectator = false;
            return true;
        }

        // Stopping follow leaves a ghost on the cockpit grid until the server
        // returns it to the battlefield. That location must not reopen the view.
        if (HasComp<GhostComponent>(user) && !HasComp<FollowerComponent>(user))
            return false;

        // Following nests through players, seats and hulls. Other passengers can
        // also see the presentation while physically inside the cockpit grid.
        var current = user;
        for (var depth = 0; depth < 16 && !TerminatingOrDeleted(current); depth++)
        {
            if (current != user && TryCrewSeat(current, out seat)) return true;
            if (TryComp(current, out FighterSeatComponent? direct) && HasCameras(direct))
            {
                seat = (current, direct);
                return true;
            }
            if (TryComp(current, out FighterGroundComponent? ground) && ground.Aircraft is { } aircraft)
                current = aircraft;
            if (TryComp(current, out FighterAircraftComponent? flight))
            {
                foreach (var candidate in new[] { flight.FrontSeat, flight.RearSeat })
                {
                    if (candidate is not { } uid || !TryComp(uid, out FighterSeatComponent? crew) || !HasCameras(crew)) continue;
                    seat = (uid, crew);
                    return true;
                }
                return false;
            }
            if (TryComp(current, out FollowerComponent? following))
                current = following.Following;
            else if (TryComp(current, out TransformComponent? transform) && transform.ParentUid.IsValid())
                current = transform.ParentUid;
            else break;
        }
        return false;
    }

    private bool TryCrewSeat(EntityUid user, out Entity<FighterSeatComponent> seat)
    {
        seat = default;
        if (!TryComp(user, out BuckleComponent? buckle) || buckle.BuckledTo is not { } uid ||
            !TryComp(uid, out FighterSeatComponent? crew) || crew.Occupant != user || !HasCameras(crew)) return false;
        seat = (uid, crew);
        return true;
    }

    private static bool HasCameras(FighterSeatComponent seat) => seat.Camera != null && seat.ExteriorCamera != null;
}
