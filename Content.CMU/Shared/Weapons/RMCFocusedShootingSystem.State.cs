using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Weapons.Ranged.AimedShot.FocusedShooting;

public sealed partial class RMCFocusedShootingSystem
{
    [SubscribeLocalEvent]
    private void OnFocusedGetState(Entity<RMCFocusedShootingComponent> ent, ref ComponentGetState args)
    {
        // Focus can outlive a killed or evolved target. State generation must remain read-only.
        TryGetNetEntity(ent.Comp.CurrentTarget, out var target);
        args.State = new CMUFocusedShootingState
        {
            CurrentTarget = target,
            FocusCounter = ent.Comp.FocusCounter,
            FocusMultiplier = ent.Comp.FocusMultiplier,
            BaseFocusMultiplier = ent.Comp.BaseFocusMultiplier,
            CurrentHealthDamageSmallXeno = ent.Comp.CurrentHealthDamageSmallXeno,
            CurrentHealthDamageXeno = ent.Comp.CurrentHealthDamageXeno,
            CurrentHealthDamageBigXeno = ent.Comp.CurrentHealthDamageBigXeno,
            BonusDamageNonXeno = ent.Comp.BonusDamageNonXeno,
            BonusDamageXeno = ent.Comp.BonusDamageXeno,
            BonusDamageBigXeno = ent.Comp.BonusDamageBigXeno,
            DazeThreshold = ent.Comp.DazeThreshold,
            DazeDuration = ent.Comp.DazeDuration,
            SlowThreshold = ent.Comp.SlowThreshold,
            LaserColor = ent.Comp.LaserColor,
        };
    }

    [SubscribeLocalEvent]
    private void OnFocusedHandleState(Entity<RMCFocusedShootingComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not CMUFocusedShootingState state)
            return;

        ent.Comp.CurrentTarget = EnsureEntity<RMCFocusedShootingComponent>(state.CurrentTarget, ent);
        ent.Comp.FocusCounter = state.FocusCounter;
        ent.Comp.FocusMultiplier = state.FocusMultiplier;
        ent.Comp.BaseFocusMultiplier = state.BaseFocusMultiplier;
        ent.Comp.CurrentHealthDamageSmallXeno = state.CurrentHealthDamageSmallXeno;
        ent.Comp.CurrentHealthDamageXeno = state.CurrentHealthDamageXeno;
        ent.Comp.CurrentHealthDamageBigXeno = state.CurrentHealthDamageBigXeno;
        ent.Comp.BonusDamageNonXeno = state.BonusDamageNonXeno;
        ent.Comp.BonusDamageXeno = state.BonusDamageXeno;
        ent.Comp.BonusDamageBigXeno = state.BonusDamageBigXeno;
        ent.Comp.DazeThreshold = state.DazeThreshold;
        ent.Comp.DazeDuration = state.DazeDuration;
        ent.Comp.SlowThreshold = state.SlowThreshold;
        ent.Comp.LaserColor = state.LaserColor;
    }
}

[Serializable, NetSerializable]
public sealed class CMUFocusedShootingState : ComponentState
{
    public NetEntity? CurrentTarget { get; init; }
    public int FocusCounter { get; init; }
    public float FocusMultiplier { get; init; }
    public float BaseFocusMultiplier { get; init; }
    public float CurrentHealthDamageSmallXeno { get; init; }
    public float CurrentHealthDamageXeno { get; init; }
    public float CurrentHealthDamageBigXeno { get; init; }
    public float BonusDamageNonXeno { get; init; }
    public float BonusDamageXeno { get; init; }
    public float BonusDamageBigXeno { get; init; }
    public float DazeThreshold { get; init; }
    public float DazeDuration { get; init; }
    public float SlowThreshold { get; init; }
    public Color LaserColor { get; init; }
}
