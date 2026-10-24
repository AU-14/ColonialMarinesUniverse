using System;
using System.Collections.Generic;
using System.Numerics;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Hygiene;

public sealed partial class CMUHygieneSystem
{
    private const int MaximumPopcornRounds = 600;
    private const int PopcornUpdateBudget = 16;
    private readonly Dictionary<EntityUid, EntityUid> _popcornSources = new();
    private readonly Queue<EntityUid> _popcornQueue = new();

    private void OnPopcornMapInit(Entity<CMULaundryPopcornComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Washer is not { } washer)
            return;
        _popcornSources[washer] = ent.Owner;
        _popcornQueue.Enqueue(ent.Owner);
    }

    private int GetPopcornCapacity(EntityUid washer)
    {
        if (_popcornSources.TryGetValue(washer, out var source)
            && !TerminatingOrDeleted(source) && !EntityManager.IsQueuedForDeletion(source)
            && TryComp<CMULaundryPopcornComponent>(source, out var popcorn))
            return Math.Max(0, MaximumPopcornRounds - popcorn.RoundsRemaining);
        return MaximumPopcornRounds;
    }

    private void OnPopcornShutdown(Entity<CMULaundryPopcornComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Washer is { } washer && _popcornSources.TryGetValue(washer, out var source) && source == ent.Owner)
            _popcornSources.Remove(washer);
    }

    private void StartLaundryPopcorn(Entity<CMULaundryComponent> washer, int rounds, EntityUid? user)
    {
        if (_popcornSources.TryGetValue(washer.Owner, out var sourceUid)
            && !TerminatingOrDeleted(sourceUid) && !EntityManager.IsQueuedForDeletion(sourceUid)
            && TryComp<CMULaundryPopcornComponent>(sourceUid, out var active))
        {
            active.RoundsRemaining += rounds;
            return;
        }

        // The emitter outlives the washer once ammunition has heated.
        var source = Spawn(null, Transform(washer.Owner).Coordinates);
        _laundryTransform.SetLocalRotation(source, Transform(washer.Owner).LocalRotation.GetCardinalDir().ToAngle());
        var popcorn = AddComp<CMULaundryPopcornComponent>(source);
        popcorn.RoundsRemaining = rounds;
        popcorn.FragmentInterval = Math.Max(0.5f, washer.Comp.AmmoFragmentInterval);
        popcorn.FragmentSpeed = washer.Comp.AmmoFragmentSpeed;
        popcorn.ConeDegrees = washer.Comp.AmmoConeDegrees;
        popcorn.PopEffect = washer.Comp.AmmoPopEffect;
        popcorn.Fragment = washer.Comp.AmmoFragment;
        popcorn.User = user;
        popcorn.Washer = washer.Owner;
        popcorn.NextPopAt = _timing.CurTime;
        popcorn.NextFragmentAt = _timing.CurTime;
        _popcornSources[washer.Owner] = source;
        _popcornQueue.Enqueue(source);
    }

    private void UpdateLaundryPopcorn()
    {
        var count = Math.Min(PopcornUpdateBudget, _popcornQueue.Count);
        while (count-- > 0)
        {
            var uid = _popcornQueue.Dequeue();
            if (TerminatingOrDeleted(uid) || EntityManager.IsQueuedForDeletion(uid)
                || !TryComp<CMULaundryPopcornComponent>(uid, out var popcorn))
                continue;
            if (popcorn.RoundsRemaining <= 0)
            {
                QueueDel(uid);
                continue;
            }
            _popcornQueue.Enqueue(uid);
            if (Paused(uid) || _timing.CurTime < popcorn.NextPopAt)
                continue;
            // Follow a moving/rotated washer while it exists. After destruction the independent
            // emitter retains its last grid-relative position and facing until all rounds pop.
            var washer = popcorn.Washer is { } machine && !TerminatingOrDeleted(machine) ? machine : uid;
            if (washer != uid)
            {
                var washerTransform = Transform(washer);
                _laundryTransform.SetCoordinates(uid, washerTransform.Coordinates);
                _laundryTransform.SetLocalRotation(uid, washerTransform.LocalRotation.GetCardinalDir().ToAngle());
            }

            var center = _laundryTransform.GetMapCoordinates(uid);
            var facing = _laundryTransform.GetWorldRotation(uid);
            // Spawn outside the washer's fixture, through the front glass on every cardinal view.
            var muzzle = new MapCoordinates(center.Position + facing.ToWorldVec() * 0.4f, center.MapId);
            var user = popcorn.User is { } actor && !TerminatingOrDeleted(actor) ? popcorn.User : null;

            // Preserve every round and the irregular schedule without dumping an entire magazine
            // into one frame after a hitch. Remaining overdue rounds continue on the next update.
            for (var popped = 0; popped < 4 && popcorn.RoundsRemaining > 0 && _timing.CurTime >= popcorn.NextPopAt; popped++)
            {
                popcorn.RoundsRemaining--;
                popcorn.NextPopAt += TimeSpan.FromSeconds(_laundryRandom.NextFloat(popcorn.MinPopInterval, popcorn.MaxPopInterval));
            }
            _audio.PlayPvs(LaundryPop, _laundryTransform.ToCoordinates(muzzle));
            Spawn(popcorn.PopEffect, muzzle);
            if (_timing.CurTime >= popcorn.NextFragmentAt)
            {
                // Individual rounds pop faster than the occasional piece of escaping casing.
                var offset = _laundryRandom.NextFloat(-popcorn.ConeDegrees / 2f, popcorn.ConeDegrees / 2f);
                var direction = (facing + Angle.FromDegrees(offset)).ToWorldVec();
                var fragment = Spawn(popcorn.Fragment, muzzle);
                _laundryGun.ShootProjectile(fragment, direction, Vector2.Zero, washer, user, speed: popcorn.FragmentSpeed);
                popcorn.NextFragmentAt = _timing.CurTime + TimeSpan.FromSeconds(popcorn.FragmentInterval);
            }
            if (popcorn.RoundsRemaining == 0)
                QueueDel(uid);
        }
    }
}
