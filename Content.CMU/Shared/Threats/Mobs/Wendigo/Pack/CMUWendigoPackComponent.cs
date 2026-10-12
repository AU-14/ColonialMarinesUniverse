using Content.Shared.Radio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Threats.Mobs.Wendigo.Pack;

/// <summary>
/// Marks a lab-made Wendigo as a member of the Wendigo pack. Pack members share a private radio
/// channel, never hurt each other, hear when a packmate dies and can watch each other.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUWendigoPackComponent : Component
{
    public static readonly ProtoId<RadioChannelPrototype> PackChannel = "CMUWendigoPack";

    /// <summary>
    /// Radio channel used as the pack's default chat channel.
    /// </summary>
    [DataField]
    public ProtoId<RadioChannelPrototype> Channel = PackChannel;

    /// <summary>
    /// Message announced to the living pack when this member dies.
    /// </summary>
    [DataField]
    public LocId DeathAnnouncement = "cmu-wendigo-pack-death-announce";

    [DataField]
    public Color AnnouncementColor = Color.FromHex("#8a5a2b");
}
