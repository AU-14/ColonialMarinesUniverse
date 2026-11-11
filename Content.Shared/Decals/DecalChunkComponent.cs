using Robust.Shared.Analyzers;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization; // CMU14: NetSerializable on delta states
using Robust.Shared.Timing; // CMU14: per decal delta tick stamps

namespace Content.Shared.Decals;

/// <summary>
/// Networked decal data attached to a chunk entity.
/// </summary>
[RegisterComponent, NetworkedComponent] // CMU14: delta states are manual below, auto generation must stay off
// [AutoGenerateComponentState(raiseAfterAutoHandleState: true, fieldDeltas: true)] // CMU14: replaced by per decal delta states, a dirty dictionary field would still resend the whole chunk
public sealed partial class DecalChunkComponent : Component
{
    public const ushort PredictedDecalCount = 256;
    public const ushort MaxServerDecalId = ushort.MaxValue - PredictedDecalCount;
    public const ushort MinPredictedDecalId = MaxServerDecalId + 1;

    /// <summary>
    /// Client predicts entities from top of the chunk index down while server goes bottom-up.
    /// This way we can minimize chances of overlap and be non-destructive to server states.
    /// </summary>
    public ushort NextPredictedDecal = ushort.MaxValue;

    // [AutoNetworkedField] // CMU14: the manual delta states below serialize this
    [DataField(customTypeSerializer: typeof(DecalChunkDecalsSerializer))]
    public Dictionary<ushort, Decal> Decals = new();

    /// <summary>
    /// Highest authoritative decal ID allocated in this chunk.
    /// </summary>
    [DataField]
    public ushort MaxDecalId;

    public List<ushort> FreeDecalIds = new();

    // CMU14 fields: per decal change ticks feeding the delta states. Server side only.
    // A modified entry dies with its decal, a removed entry dies when the id is reused.
    [NonSerialized] public readonly Dictionary<ushort, GameTick> ModifiedTicks = new();
    [NonSerialized] public readonly Dictionary<ushort, GameTick> RemovedTicks = new();
}

// CMU14 class
[Serializable, NetSerializable]
public sealed class DecalChunkState(Dictionary<ushort, Decal> decals) : ComponentState
{
    public Dictionary<ushort, Decal> Decals = decals;
}

// CMU14 class
[Serializable, NetSerializable]
public sealed class DecalChunkDeltaState(
    Dictionary<ushort, Decal> modifiedDecals,
    List<ushort> removedDecals)
    : ComponentState, IComponentDeltaState<DecalChunkState>
{
    public Dictionary<ushort, Decal> ModifiedDecals = modifiedDecals;
    public List<ushort> RemovedDecals = removedDecals;

    public void ApplyToFullState(DecalChunkState state)
    {
        foreach (var (id, decal) in ModifiedDecals)
            state.Decals[id] = decal;

        foreach (var id in RemovedDecals)
            state.Decals.Remove(id);
    }

    public DecalChunkState CreateNewFullState(DecalChunkState state)
    {
        var full = new DecalChunkState(new Dictionary<ushort, Decal>(state.Decals));
        ApplyToFullState(full);
        return full;
    }
}
