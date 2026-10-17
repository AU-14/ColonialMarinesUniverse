using Content.Shared.StepTrigger.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.StepTrigger.Systems;

public sealed partial class StepTriggerSystem
{
    [SubscribeLocalEvent]
    private void OnStepTriggerGetState(Entity<StepTriggerComponent> ent, ref ComponentGetState args)
    {
        var colliding = new HashSet<NetEntity>();
        foreach (var uid in ent.Comp.Colliding)
        {
            if (TryGetNetEntity(uid, out var net))
                colliding.Add(net.Value);
        }

        var steppedOn = new HashSet<NetEntity>();
        foreach (var uid in ent.Comp.CurrentlySteppedOn)
        {
            if (TryGetNetEntity(uid, out var net))
                steppedOn.Add(net.Value);
        }

        args.State = new CMUStepTriggerState(colliding, steppedOn, ent.Comp.Active,
            ent.Comp.IntersectRatio, ent.Comp.RequiredTriggeredSpeed, ent.Comp.IgnoreWeightless, ent.Comp.StepOn);
    }

    [SubscribeLocalEvent]
    private void OnStepTriggerHandleState(Entity<StepTriggerComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not CMUStepTriggerState state)
            return;

        EnsureEntitySet<StepTriggerComponent>(state.Colliding, ent, ent.Comp.Colliding);
        EnsureEntitySet<StepTriggerComponent>(state.CurrentlySteppedOn, ent, ent.Comp.CurrentlySteppedOn);
        ent.Comp.Active = state.Active;
        ent.Comp.IntersectRatio = state.IntersectRatio;
        ent.Comp.RequiredTriggeredSpeed = state.RequiredTriggeredSpeed;
        ent.Comp.IgnoreWeightless = state.IgnoreWeightless;
        ent.Comp.StepOn = state.StepOn;
        // Prediction still needs the active marker to follow the restored collision set.
        RefreshActiveTrigger(ent, ent.Comp);
    }
}

[Serializable, NetSerializable]
public sealed class CMUStepTriggerState(
    HashSet<NetEntity> colliding,
    HashSet<NetEntity> currentlySteppedOn,
    bool active,
    float intersectRatio,
    float requiredTriggeredSpeed,
    bool ignoreWeightless,
    bool stepOn) : ComponentState
{
    public HashSet<NetEntity> Colliding { get; } = colliding;
    public HashSet<NetEntity> CurrentlySteppedOn { get; } = currentlySteppedOn;
    public bool Active { get; } = active;
    public float IntersectRatio { get; } = intersectRatio;
    public float RequiredTriggeredSpeed { get; } = requiredTriggeredSpeed;
    public bool IgnoreWeightless { get; } = ignoreWeightless;
    public bool StepOn { get; } = stepOn;
}
