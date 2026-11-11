using Content.Server._RMC14.Vehicle;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Explosion;

public sealed partial class CMULungeMineSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private RMCDazedSystem _dazed = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private TankCookOffSystem _tankCookOff = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TriggerSystem _trigger = default!;

    // Severing an arm or leg also takes the hand or foot below it; that later part then no longer resolves,
    // so it fails and does not count toward the cap.
    private static readonly CMUMedicalBodyPartKey[] LimbCandidates =
    {
        new(BodyPartType.Arm, BodyPartSymmetry.Left),
        new(BodyPartType.Arm, BodyPartSymmetry.Right),
        new(BodyPartType.Hand, BodyPartSymmetry.Left),
        new(BodyPartType.Hand, BodyPartSymmetry.Right),
        new(BodyPartType.Leg, BodyPartSymmetry.Left),
        new(BodyPartType.Leg, BodyPartSymmetry.Right),
        new(BodyPartType.Foot, BodyPartSymmetry.Left),
        new(BodyPartType.Foot, BodyPartSymmetry.Right),
    };

    private readonly HashSet<Entity<TankCookOffComponent>> _tanks = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMULungeMineComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<CMULungeMineComponent, TriggerEvent>(OnTrigger);
    }

    private void OnMeleeHit(Entity<CMULungeMineComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        foreach (var target in args.HitEntities)
        {
            if (target == args.User)
                continue;

            _audio.PlayPvs(ent.Comp.TriggerSound, ent);
            // Detonate at the target rather than in the wielder's hand.
            _transform.SetCoordinates(ent, Transform(target).Coordinates);
            _trigger.Trigger(ent, args.User);
            return;
        }
    }

    private void OnTrigger(Entity<CMULungeMineComponent> ent, ref TriggerEvent args)
    {
        RollTankCookOff(ent);

        if (args.User is not { } user || TerminatingOrDeleted(user))
            return;

        _dazed.TryDaze(user, ent.Comp.DazeDuration, true, stutter: true);

        if (ent.Comp.SeversWielderLimbs)
            SeverRandomLimbs(user, _random.Next(1, ent.Comp.MaxSeveredLimbs + 1));
    }

    private void RollTankCookOff(Entity<CMULungeMineComponent> ent)
    {
        if (ent.Comp.CookOffChance <= 0)
            return;

        _tanks.Clear();
        _lookup.GetEntitiesInRange(_transform.GetMapCoordinates(ent), ent.Comp.CookOffRange, _tanks);
        foreach (var tank in _tanks)
        {
            if (_random.Prob(Math.Clamp(ent.Comp.CookOffChance, 0, 1)))
                _tankCookOff.TryStartCookOff(tank);
        }
    }

    private void SeverRandomLimbs(EntityUid body, int count)
    {
        var candidates = new List<CMUMedicalBodyPartKey>(LimbCandidates);
        _random.Shuffle(candidates);

        var severed = 0;
        foreach (var key in candidates)
        {
            if (severed >= count)
                break;

            if (!_medicalIndex.TryGetBodyPart(body, key, out var part))
                continue;

            var ev = new BodyPartSeverAttemptEvent(body, part, key.Type);
            RaiseLocalEvent(part, ref ev, broadcast: true);
            if (ev.Succeeded)
                severed++;
        }
    }
}
