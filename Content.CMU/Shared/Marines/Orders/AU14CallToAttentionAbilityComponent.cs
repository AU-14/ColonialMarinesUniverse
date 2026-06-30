using Content.Shared.Chat.Prototypes;
using Content.Shared.Examine;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._AU14.Marines.Orders;

/// <summary>
/// Grants the Call to Attention order ability.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AU14CallToAttentionAbilityComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId Action = "ActionMarineCallToAttention";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    [DataField, AutoNetworkedField]
    public ProtoId<EmotePrototype> Emote = "AU14SnapToAttention";

    [DataField]
    public List<LocId> Callouts = new()
    {
        "au14-call-to-attention-callout-room-attention",
        "au14-call-to-attention-callout-room-tenchut",
        "au14-call-to-attention-callout-commander-on-deck",
    };

    /// <summary>
    /// Cooldown in seconds between uses.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(60);

    /// <summary>
    /// How long non-exempt targets are forced to whisper after the order.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan WhisperDuration = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Maximum sight range of the effect in tiles.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Range = ExamineSystemShared.MaxRaycastRange;

    /// <summary>
    /// Maximum random delay before each nearby humanoid responds.
    /// </summary>
    [DataField]
    public TimeSpan ResponseStagger = TimeSpan.FromSeconds(1.5);
}
