using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// How a player's camera is turned, which only their client knows: it sends it over, so a mounted gun
/// they fire shoots from where their screen draws its barrel. Not networked to anyone else.
/// </summary>
[RegisterComponent]
public sealed partial class CMUViewRotationComponent : Component
{
    [ViewVariables]
    public Angle Rotation;
}

/// <summary>
/// A client's camera rotation, sent when it turns.
/// </summary>
[Serializable, NetSerializable]
public sealed class CMUViewRotationEvent(Angle rotation) : EntityEventArgs
{
    public readonly Angle Rotation = rotation;
}
