using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CombatMode;

public abstract partial class SharedCombatModeSystem
{
    private void OnCombatModeGetState(Entity<CombatModeComponent> ent, ref ComponentGetState args)
    {
        TryGetNetEntity(ent.Comp.CombatToggleActionEntity, out var action);
        args.State = new CombatModeComponentState
        {
            CombatToggleActionEntity = action,
            IsInCombatMode = ent.Comp.IsInCombatMode,
            ToggleMouseRotator = ent.Comp.ToggleMouseRotator,
        };
    }

    private void OnCombatModeHandleState(Entity<CombatModeComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not CombatModeComponentState state)
            return;

        ent.Comp.CombatToggleActionEntity = EnsureEntity<CombatModeComponent>(state.CombatToggleActionEntity, ent);
        ent.Comp.IsInCombatMode = state.IsInCombatMode;
        ent.Comp.ToggleMouseRotator = state.ToggleMouseRotator;

        var handled = new CombatModeStateAppliedEvent();
        RaiseLocalEvent(ent, ref handled);
    }
}

[ByRefEvent]
public readonly record struct CombatModeStateAppliedEvent;

[Serializable, NetSerializable]
public sealed class CombatModeComponentState : ComponentState
{
    public NetEntity? CombatToggleActionEntity { get; init; }
    public bool IsInCombatMode { get; init; }
    public bool ToggleMouseRotator { get; init; }
}
