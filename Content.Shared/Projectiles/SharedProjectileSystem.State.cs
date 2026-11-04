using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Projectiles;

public abstract partial class SharedProjectileSystem
{
    private void OnProjectileGetState(Entity<ProjectileComponent> ent, ref ComponentGetState args)
    {
        // These references can outlive their entities; a missing entity is an invalid network reference.
        TryGetNetEntity(ent.Comp.Shooter, out var shooter);
        TryGetNetEntity(ent.Comp.Weapon, out var weapon);

        args.State = new ProjectileComponentState
        {
            Angle = ent.Comp.Angle,
            Shooter = shooter,
            Weapon = weapon,
            DelayToAcknowledgeShooter = ent.Comp.DelayToAcknowledgeShooter,
            ProjectileSpent = ent.Comp.ProjectileSpent,
            MaxFixedRange = ent.Comp.MaxFixedRange,
        };
    }

    private void OnProjectileHandleState(Entity<ProjectileComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not ProjectileComponentState state)
            return;

        ent.Comp.Angle = state.Angle;
        ent.Comp.Shooter = EnsureEntity<ProjectileComponent>(state.Shooter, ent);
        ent.Comp.Weapon = EnsureEntity<ProjectileComponent>(state.Weapon, ent);
        ent.Comp.DelayToAcknowledgeShooter = state.DelayToAcknowledgeShooter;
        ent.Comp.ProjectileSpent = state.ProjectileSpent;
        ent.Comp.MaxFixedRange = state.MaxFixedRange;
    }
}

[Serializable, NetSerializable]
public sealed class ProjectileComponentState : ComponentState
{
    public Angle Angle { get; init; }
    public NetEntity? Shooter { get; init; }
    public NetEntity? Weapon { get; init; }
    public TimeSpan DelayToAcknowledgeShooter { get; init; }
    public bool ProjectileSpent { get; init; }
    public float? MaxFixedRange { get; init; }
}
