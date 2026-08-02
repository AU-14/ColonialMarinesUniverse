using Content.Shared._AU14.Radio;
using Content.Shared.Damage;

namespace Content.Server._AU14.Radio;

public sealed partial class AU14CommsStructureVisualsSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AU14CommsStructureVisualsComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(Entity<AU14CommsStructureVisualsComponent> ent, ref DamageChangedEvent args)
    {
        _appearance.SetData(ent.Owner, AU14CommsStructureVisuals.Damaged,
            args.Damageable.TotalDamage >= ent.Comp.DamagedAt);
    }
}
