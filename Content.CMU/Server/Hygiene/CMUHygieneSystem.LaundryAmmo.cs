using System;
using System.Numerics;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Hygiene;

public sealed partial class CMUHygieneSystem
{
    private void StartLaundryPopcorn(Entity<CMULaundryComponent> washer, int rounds, EntityUid? user)
    {
        var existing = EntityQueryEnumerator<CMULaundryPopcornComponent>();
        while (existing.MoveNext(out var sourceUid, out var active))
        {
            if (active.Washer != washer.Owner || active.RoundsRemaining <= 0 || TerminatingOrDeleted(sourceUid))
                continue;
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
    }

    private void UpdateLaundryPopcorn()
    {
        var query = EntityQueryEnumerator<CMULaundryPopcornComponent>();
        while (query.MoveNext(out var uid, out var popcorn))
        {
            if (_timing.CurTime < popcorn.NextPopAt)
                continue;
            if (popcorn.RoundsRemaining <= 0)
            {
                QueueDel(uid);
                continue;
            }
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
