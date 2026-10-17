using Content.Shared.Trigger.Components;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Trigger.Systems;

public sealed partial class TriggerSystem
{
    private void OnTimerGetState(Entity<TimerTriggerComponent> ent, ref ComponentGetState args)
    {
        // The timer can outlive the entity that activated it.
        TryGetNetEntity(ent.Comp.User, out var user);
        args.State = new TimerTriggerComponentState
        {
            KeysIn = new(ent.Comp.KeysIn),
            KeyOut = ent.Comp.KeyOut,
            Delay = ent.Comp.Delay,
            DelayOptions = new(ent.Comp.DelayOptions),
            NextTrigger = ent.Comp.NextTrigger,
            BeepInterval = ent.Comp.BeepInterval,
            User = user,
            BeepSound = ent.Comp.BeepSound,
            Examinable = ent.Comp.Examinable,
            Popup = ent.Comp.Popup,
        };
    }

    private void OnTimerHandleState(Entity<TimerTriggerComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not TimerTriggerComponentState state)
            return;

        ent.Comp.KeysIn = new(state.KeysIn);
        ent.Comp.KeyOut = state.KeyOut;
        ent.Comp.Delay = state.Delay;
        ent.Comp.DelayOptions = new(state.DelayOptions);
        ent.Comp.NextTrigger = state.NextTrigger;
        ent.Comp.BeepInterval = state.BeepInterval;
        ent.Comp.User = EnsureEntity<TimerTriggerComponent>(state.User, ent);
        ent.Comp.BeepSound = state.BeepSound;
        ent.Comp.Examinable = state.Examinable;
        ent.Comp.Popup = state.Popup;
    }
}

[Serializable, NetSerializable]
public sealed class TimerTriggerComponentState : ComponentState
{
    public List<string> KeysIn { get; init; } = new();
    public string? KeyOut { get; init; }
    public TimeSpan Delay { get; init; }
    public List<TimeSpan> DelayOptions { get; init; } = new();
    public TimeSpan NextTrigger { get; init; }
    public TimeSpan BeepInterval { get; init; }
    public NetEntity? User { get; init; }
    public SoundSpecifier? BeepSound { get; init; }
    public bool Examinable { get; init; }
    public LocId? Popup { get; init; }
}
