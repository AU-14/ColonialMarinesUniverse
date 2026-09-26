using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Hijack;

[Serializable, NetSerializable]
public sealed class CMUShipCinematicEvent(CMUShipCinematicStage stage) : EntityEventArgs
{
    public CMUShipCinematicStage Stage = stage;
}

[Serializable, NetSerializable]
public enum CMUShipCinematicStage : byte
{
    Ship,
    Detonation,
    Destroyed,
    Clear,
}

[ByRefEvent]
public record struct CMUShipRoundEndAttemptEvent
{
    public bool Cancelled;
}
