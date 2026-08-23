using Content.Shared.Buckle.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Robust.Shared.Map;

namespace Content.Server._CMU14.Yautja;

public sealed class YautjaTeleportSystem : EntitySystem
{
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public bool TeleportTrain(EntityUid user, MapCoordinates coordinates)
    {
        if (Deleted(user) || IsAnchored(user))
            return false;

        var train = new List<EntityUid> { user };
        var links = new List<(EntityUid Puller, EntityUid Pulled)>();
        var seen = new HashSet<EntityUid> { user };
        var current = user;
        EntityUid? stoppedExternalPuller = null;

        AddBuckledUserStrap(user, train, seen);

        while (TryComp(current, out PullerComponent? puller) && puller.Pulling is { } pulled)
        {
            if (seen.Contains(pulled) || Deleted(pulled) || IsAnchored(pulled))
            {
                if (!Deleted(pulled) && IsAnchored(pulled))
                    StopPullLink(pulled);

                break;
            }

            train.Add(pulled);
            seen.Add(pulled);
            if (!AddPulledMobBuckledStrap(pulled, train, seen))
            {
                StopPullLink(pulled);
                break;
            }

            links.Add((current, pulled));
            AddPulledObjectBuckledPassengers(pulled, train, seen);
            current = pulled;
        }

        if (TryComp(user, out PullableComponent? pulledUser) &&
            pulledUser.Puller is { } externalPuller)
        {
            _pulling.TryStopPull(user, pulledUser, externalPuller);

            if (!seen.Contains(externalPuller))
                stoppedExternalPuller = externalPuller;
        }

        foreach (var (_, pulled) in links)
        {
            if (IsBuckledToEntityInTrain(pulled, seen))
                continue;

            if (TryComp(pulled, out PullableComponent? pullable) &&
                pullable.Puller is { } linkPuller &&
                !IsBuckledToEntityInTrain(linkPuller, seen))
            {
                _pulling.TryStopPull(pulled, pullable, pullable.Puller.Value);
            }
        }

        foreach (var entity in train)
        {
            if (IsBuckledToEntityInTrain(entity, seen))
                continue;

            if (!Deleted(entity))
                _transform.SetMapCoordinates(entity, coordinates);
        }

        foreach (var entity in train)
        {
            if (!IsBuckledToEntityInTrain(entity, seen) || Deleted(entity))
                continue;

            RaiseMovedForCarriedBuckledTrainMember(entity);
        }

        foreach (var (puller, pulled) in links)
        {
            if (puller == stoppedExternalPuller && pulled == user)
                continue;

            if (IsBuckledToEntityInTrain(pulled, seen))
                continue;

            if (IsBuckledToEntityInTrain(puller, seen))
                continue;

            if (!Deleted(puller) && !Deleted(pulled))
                _pulling.TryStartPull(puller, pulled);
        }

        return true;
    }

    private void AddBuckledUserStrap(EntityUid user, List<EntityUid> train, HashSet<EntityUid> seen)
    {
        if (!TryComp(user, out BuckleComponent? buckle) ||
            buckle.BuckledTo is not { } strap ||
            Deleted(strap) ||
            !HasComp<StrapComponent>(strap))
        {
            return;
        }

        if (IsAnchored(strap))
            return;

        if (seen.Add(strap))
            train.Add(strap);
    }

    private void AddPulledObjectBuckledPassengers(EntityUid pulled, List<EntityUid> train, HashSet<EntityUid> seen)
    {
        if (!TryComp(pulled, out StrapComponent? strap))
            return;

        foreach (var buckled in strap.BuckledEntities)
        {
            if (Deleted(buckled))
                continue;

            if (seen.Add(buckled))
                train.Add(buckled);

            if (TryComp(buckled, out PullerComponent? puller) &&
                puller.Pulling is { } passengerPulled &&
                TryComp(passengerPulled, out PullableComponent? passengerPulledPullable))
            {
                _pulling.TryStopPull(passengerPulled, passengerPulledPullable);
            }
        }
    }

    private bool AddPulledMobBuckledStrap(EntityUid pulled, List<EntityUid> train, HashSet<EntityUid> seen)
    {
        if (!TryComp(pulled, out BuckleComponent? buckle) ||
            buckle.BuckledTo is not { } strap ||
            Deleted(strap) ||
            !HasComp<StrapComponent>(strap))
        {
            return true;
        }

        if (IsAnchored(strap))
        {
            train.Remove(pulled);
            seen.Remove(pulled);
            return false;
        }

        if (seen.Add(strap))
            train.Add(strap);

        return true;
    }

    private bool IsBuckledToEntityInTrain(EntityUid entity, HashSet<EntityUid> train)
    {
        return TryComp(entity, out BuckleComponent? buckle) &&
            buckle.BuckledTo is { } strap &&
            train.Contains(strap);
    }

    private void RaiseMovedForCarriedBuckledTrainMember(EntityUid entity)
    {
        var xform = Transform(entity);
        var meta = MetaData(entity);
        var coordinates = xform.Coordinates;
        var rotation = xform.LocalRotation;
        var ev = new MoveEvent((entity, xform, meta), coordinates, coordinates, rotation, rotation);
        RaiseLocalEvent(entity, ref ev);
    }

    private void StopPullLink(EntityUid pulled)
    {
        if (TryComp(pulled, out PullableComponent? pullable))
            _pulling.TryStopPull(pulled, pullable);
    }

    private bool IsAnchored(EntityUid uid)
    {
        return TryComp(uid, out TransformComponent? transform) && transform.Anchored;
    }
}
