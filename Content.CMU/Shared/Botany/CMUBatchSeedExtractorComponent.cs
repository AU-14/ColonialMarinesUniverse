using Content.Shared.Destructible.Thresholds;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Botany;

/// <summary>
/// CMU14: a seed extractor that works in batches, like the window-machine grinder. Produce goes into the input slots
/// (by hand, through the window or by an inserter); once the machine has power, produce and room in its output slots, it
/// runs for <see cref="WorkTime"/> and turns every loaded produce into seed packets that keep the produce's plant snapshot.
/// Replaces the upstream <c>SeedExtractor</c> component on the <c>SeedExtractor</c> prototype.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUBatchSeedExtractorComponent : Component
{
    /// <summary>
    /// Packets made from one produce, both ends inclusive. Also used by the plant-bag verb.
    /// </summary>
    [DataField]
    public MinMax BaseSeeds = new(1, 3);

    [DataField]
    public TimeSpan WorkTime = TimeSpan.FromSeconds(4);

    [DataField]
    public float WorkTimeMultiplier = 1f;

    /// <summary>
    /// Whether a batch starts by itself when there is produce and room, or only when asked through the window.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AutoStart = true;

    /// <summary>
    /// Input slots are named <see cref="InputSlotPrefix"/>1..<see cref="InputSlotCount"/>.
    /// </summary>
    [DataField]
    public string InputSlotPrefix = "produce";

    [DataField]
    public int InputSlotCount = 8; // CMU14 (ECS-06): 8 produce a batch

    /// <summary>
    /// Output slots are named <see cref="OutputSlotPrefix"/>1..<see cref="OutputSlotCount"/>.
    /// </summary>
    [DataField]
    public string OutputSlotPrefix = "seeds";

    [DataField]
    public int OutputSlotCount = 16; // CMU14 (ECS-06): kept at twice the inputs

    /// <summary>
    /// When the running batch ends. Null while idle or paused.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan? EndTime;

    /// <summary>
    /// Time left of a batch that was paused by a power loss.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan? Remaining;

    /// <summary>
    /// The full length of the running batch, for the progress bar.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan? Duration;

    /// <summary>
    /// A batch is under way (running or paused).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Active;

    public string InputSlotId(int index)
    {
        return $"{InputSlotPrefix}{index + 1}";
    }

    public string OutputSlotId(int index)
    {
        return $"{OutputSlotPrefix}{index + 1}";
    }
}

[Serializable, NetSerializable]
public enum CMUBatchSeedExtractorUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CMUBatchSeedExtractorStartMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CMUBatchSeedExtractorEjectAllMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CMUBatchSeedExtractorToggleAutoMessage : BoundUserInterfaceMessage;
