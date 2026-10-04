using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.ZLevels.Core.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class CMUZVisualFollowerComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Target;

    public Vector2? OriginalOffset;

    public Vector2 AppliedOffset;
}
