using Content.Shared.CMU14.Radio;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;

namespace Content.Server.CMU14.Radio;

public sealed partial class AU14CommsStructureVisualsSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AU14CommsStructureVisualsComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(Entity<AU14CommsStructureVisualsComponent> ent, ref DamageChangedEvent args)
    {
        _appearance.SetData(ent.Owner, AU14CommsStructureVisuals.Damaged,
            _damageable.GetTotalDamage((ent.Owner, args.Damageable)) >= ent.Comp.DamagedAt);
    }
}
