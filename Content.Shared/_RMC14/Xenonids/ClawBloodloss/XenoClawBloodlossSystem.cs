using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared._RMC14.Xenonids.ClawBloodloss;

public sealed partial class XenoClawBloodlossSystem : EntitySystem
{
    [Dependancy] private BloodstreamSystem _bloodstream = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoClawBloodlossComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(Entity<XenoClawBloodlossComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || ent.Comp.Bloodloss <= FixedPoint2.Zero)
          return;

        foreach (var hit in args.HitEntities)
        {
            if TryComp<BloodstreamComponent>(hit, out var bloodstream))
              _bloodstream.TryModifyBloodLevel((hit, bloodstream), -ent.Comp.Bloodloss);
        }
    }
}
