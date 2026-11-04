using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameStates;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    private void InitializeTargetedProjectile()
    {
        SubscribeLocalEvent<TargetedProjectileComponent, ComponentGetState>(OnTargetedProjectileGetState);
        SubscribeLocalEvent<TargetedProjectileComponent, ComponentHandleState>(OnTargetedProjectileHandleState);
    }

    private void OnTargetedProjectileGetState(
        Entity<TargetedProjectileComponent> ent,
        ref ComponentGetState args)
    {
        TryGetNetEntity(ent.Comp.Target, out var target);
        args.State = new TargetedProjectileComponentState
        {
            Target = target ?? NetEntity.Invalid,
        };
    }

    private void OnTargetedProjectileHandleState(
        Entity<TargetedProjectileComponent> ent,
        ref ComponentHandleState args)
    {
        if (args.Current is not TargetedProjectileComponentState state)
            return;

        ent.Comp.Target = EnsureEntity<TargetedProjectileComponent>(state.Target, ent);
    }
}
