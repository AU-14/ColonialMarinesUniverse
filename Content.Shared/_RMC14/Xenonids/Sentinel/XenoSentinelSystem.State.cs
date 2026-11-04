using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Xenonids.Sentinel;

public sealed partial class XenoSentinelSystem
{
    private void OnIntoxicatedGetState(Entity<XenoIntoxicatedComponent> ent, ref ComponentGetState args)
    {
        // These references can outlive their entities; a missing entity is an invalid network reference.
        TryGetNetEntity(ent.Comp.LastSource, out var lastSource);

        args.State = new XenoIntoxicatedComponentState
        {
            Stacks = ent.Comp.Stacks,
            MaxStacks = ent.Comp.MaxStacks,
            NextTick = ent.Comp.NextTick,
            TickEvery = ent.Comp.TickEvery,
            TickBaseDamage = ent.Comp.TickBaseDamage,
            TickDamageStackDivisor = ent.Comp.TickDamageStackDivisor,
            TickDecay = ent.Comp.TickDecay,
            HighStackThreshold = ent.Comp.HighStackThreshold,
            HighStackSlowAtThreshold = ent.Comp.HighStackSlowAtThreshold,
            HighStackSlowAtMax = ent.Comp.HighStackSlowAtMax,
            ResistReduction = ent.Comp.ResistReduction,
            ResistDuration = ent.Comp.ResistDuration,
            LastSource = lastSource,
        };
    }

    private void OnIntoxicatedHandleState(Entity<XenoIntoxicatedComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not XenoIntoxicatedComponentState state)
            return;

        ent.Comp.Stacks = state.Stacks;
        ent.Comp.MaxStacks = state.MaxStacks;
        ent.Comp.NextTick = state.NextTick;
        ent.Comp.TickEvery = state.TickEvery;
        ent.Comp.TickBaseDamage = state.TickBaseDamage;
        ent.Comp.TickDamageStackDivisor = state.TickDamageStackDivisor;
        ent.Comp.TickDecay = state.TickDecay;
        ent.Comp.HighStackThreshold = state.HighStackThreshold;
        ent.Comp.HighStackSlowAtThreshold = state.HighStackSlowAtThreshold;
        ent.Comp.HighStackSlowAtMax = state.HighStackSlowAtMax;
        ent.Comp.ResistReduction = state.ResistReduction;
        ent.Comp.ResistDuration = state.ResistDuration;
        ent.Comp.LastSource = EnsureEntity<XenoIntoxicatedComponent>(state.LastSource, ent);

        _movementSpeed.RefreshMovementSpeedModifiers((ent.Owner, null));
    }
}

[Serializable, NetSerializable]
public sealed class XenoIntoxicatedComponentState : ComponentState
{
    public int Stacks { get; init; }
    public int MaxStacks { get; init; }
    public TimeSpan NextTick { get; init; }
    public TimeSpan TickEvery { get; init; }
    public FixedPoint2 TickBaseDamage { get; init; }
    public float TickDamageStackDivisor { get; init; }
    public int TickDecay { get; init; }
    public int HighStackThreshold { get; init; }
    public float HighStackSlowAtThreshold { get; init; }
    public float HighStackSlowAtMax { get; init; }
    public int ResistReduction { get; init; }
    public TimeSpan ResistDuration { get; init; }
    public NetEntity? LastSource { get; init; }
}
