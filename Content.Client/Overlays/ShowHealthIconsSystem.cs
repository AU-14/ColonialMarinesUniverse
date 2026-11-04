using Content.Client._RMC14.Medical.HUD;
using Content.Shared.Damage.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.Overlays;
using Content.Shared.StatusIcon.Components;

namespace Content.Client.Overlays;

/// <summary>
/// Shows a healthy icon on mobs.
/// </summary>
public sealed partial class ShowHealthIconsSystem : EquipmentHudSystem<ShowHealthIconsComponent>
{
    [Dependency] private CMHealthIconsSystem _healthIcons = default!;
    [ViewVariables]
    public HashSet<string> DamageContainers = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InjurableComponent, GetStatusIconsEvent>(OnGetStatusIconsEvent);
        SubscribeLocalEvent<ShowHealthIconsComponent, AfterAutoHandleStateEvent>(OnHandleState);
    }

    protected override void UpdateInternal(RefreshEquipmentHudEvent<ShowHealthIconsComponent> component)
    {
        base.UpdateInternal(component);

        DamageContainers.Clear();
        foreach (var comp in component.Components)
        {
            foreach (var damageContainerId in comp.DamageContainers)
            {
                DamageContainers.Add(damageContainerId);
            }
        }
    }

    protected override void DeactivateInternal()
    {
        base.DeactivateInternal();

        DamageContainers.Clear();
    }

    private void OnHandleState(Entity<ShowHealthIconsComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        RefreshOverlay();
    }

    private void OnGetStatusIconsEvent(Entity<InjurableComponent> entity, ref GetStatusIconsEvent args)
    {
        if (!IsActive)
            return;

        if (entity.Comp.DamageContainer == null ||
            !DamageContainers.Contains(entity.Comp.DamageContainer) ||
            !TryComp(entity, out DamageableComponent? damageable))
        {
            return;
        }

        if (_healthIcons.TryGetIcon((entity.Owner, damageable), out var healthIcon))
            args.StatusIcons.Add(healthIcon);
    }
}
