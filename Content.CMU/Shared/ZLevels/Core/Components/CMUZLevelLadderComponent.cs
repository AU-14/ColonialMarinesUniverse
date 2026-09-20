using Robust.Shared.GameStates;
using System.Numerics;
using Content.Shared.Interaction;
using Robust.Shared.Audio;

namespace Content.Shared.CMU14.ZLevels.Core.Components;

/// <summary>
/// Moves a user by a relative Z-level offset when activated.
/// Unlike the RMC ladder, this resolves through the current Z-level network instead of a linked destination entity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUZLevelLadderComponent : Component
{
    /// <summary>
    /// How long it takes to climb the ladder.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan Delay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Maximum distance from the ladder before the climb is cancelled.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Range = SharedInteractionSystem.InteractionRange + 0.1f;

    /// <summary>
    /// Relative Z-level offset to move the user by. Usually 1 for up or -1 for down.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Offset = 1;

    /// <summary>A through ladder can also be climbed in the other direction.</summary>
    [DataField, AutoNetworkedField]
    public int? AdditionalOffset;

    /// <summary>Offset from this ladder to its primary exit, in the deck's local axes.</summary>
    [DataField, AutoNetworkedField]
    public Vector2 LandingOffset;

    /// <summary>Exit offset when climbing towards AdditionalOffset.</summary>
    [DataField, AutoNetworkedField]
    public Vector2 AdditionalLandingOffset;

    /// <summary>
    /// Local Z position to apply after the move. A small positive value lets the user rest on a lower-level ladder top.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float LandingLocalPosition = 0.05f;

    [DataField]
    public SoundSpecifier? StartSound;

    [DataField]
    public SoundSpecifier? FinishSound;
}
