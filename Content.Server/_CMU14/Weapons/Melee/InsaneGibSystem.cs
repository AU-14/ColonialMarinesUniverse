using Content.Shared._CMU14.Weapons.Melee;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;

namespace Content.Server._CMU14.Weapons.Melee;

public sealed class InsaneGibSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<InsaneGibComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(Entity<InsaneGibComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        var gibbed = false;
        foreach (var target in args.HitEntities)
        {
            if (!TryComp<BodyComponent>(target, out var body))
                continue;

            gibbed = true;
            var coords = _transform.GetMoverCoordinates(target);
            foreach (var (proto, count) in ent.Comp.ExtraGibs)
            {
                var n = _random.Next(count.Min, count.Max + 1);
                for (var i = 0; i < n; i++)
                {
                    var gib = Spawn(proto, coords.Offset(_random.NextVector2(ent.Comp.SpawnOffset)));
                    _transform.SetWorldRotation(gib, _random.NextAngle());
                    if (TryComp<PhysicsComponent>(gib, out var physics))
                        _physics.ApplyLinearImpulse(gib, _random.NextAngle().ToVec() * (ent.Comp.ExtraGibImpulse + _random.NextFloat(8f)));
                }
            }

            _body.GibBody(target, true, body, splatModifier: ent.Comp.SplatModifier);
        }

        // the body is already paste, skip the redundant damage pass
        args.Handled = gibbed;
    }
}
