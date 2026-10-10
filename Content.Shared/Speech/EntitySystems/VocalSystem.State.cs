using Content.Shared.Chat.Prototypes;
using Content.Shared.Speech.Components;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Speech.EntitySystems;

public sealed partial class VocalSystem
{
    [SubscribeLocalEvent]
    private void OnGetState(Entity<VocalComponent> ent, ref ComponentGetState args)
    {
        // Actions can be deleted before their owner is removed during round cleanup.
        TryGetNetEntity(ent.Comp.EmoteActionEntity, out var action);
        args.State = new VocalComponentState
        {
            ScreamId = ent.Comp.ScreamId,
            Wilhelm = ent.Comp.Wilhelm,
            WilhelmProbability = ent.Comp.WilhelmProbability,
            EmoteAction = ent.Comp.EmoteAction,
            EmoteActionEntity = action,
            EmoteSounds = ent.Comp.EmoteSounds,
        };
    }

    [SubscribeLocalEvent]
    private void OnHandleState(Entity<VocalComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not VocalComponentState state)
            return;

        ent.Comp.ScreamId = state.ScreamId;
        ent.Comp.Wilhelm = state.Wilhelm;
        ent.Comp.WilhelmProbability = state.WilhelmProbability;
        ent.Comp.EmoteAction = state.EmoteAction;
        ent.Comp.EmoteActionEntity = EnsureEntity<VocalComponent>(state.EmoteActionEntity, ent);
        ent.Comp.EmoteSounds = state.EmoteSounds;
    }
}

[Serializable, NetSerializable]
public sealed class VocalComponentState : ComponentState
{
    public ProtoId<EmotePrototype> ScreamId { get; init; }
    public SoundSpecifier Wilhelm { get; init; } = default!;
    public float WilhelmProbability { get; init; }
    public EntProtoId? EmoteAction { get; init; }
    public NetEntity? EmoteActionEntity { get; init; }
    public ProtoId<EmoteSoundsPrototype>? EmoteSounds { get; init; }
}
