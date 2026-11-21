using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Mutiny;

// rides along with MutineerLeaderComponent, which admins add in code, so every number here is a C# default
[RegisterComponent]
public sealed partial class CMUMutinyLeaderComponent : Component
{
    [DataField]
    public bool Begun;

    [DataField]
    public EntProtoId BeginAction = "CMUActionMutinyBegin";

    [DataField]
    public EntityUid? BeginActionEntity;

    [DataField]
    public TimeSpan InviteTime = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan ChooseSideTime = TimeSpan.FromSeconds(30);

    // stops the leader spamming the same marine with invites after a no
    [DataField]
    public TimeSpan DeclineCooldown = TimeSpan.FromSeconds(60);

    [DataField]
    public SoundSpecifier BeginSound = new SoundPathSpecifier("/Audio/_RMC14/Announcements/ARES/attention_jingle.ogg");
}

// accepted an invite before the mutiny went loud. server only so nobody can read it off the wire
[RegisterComponent]
public sealed partial class CMUMutinySwornComponent : Component
{
    [DataField]
    public EntityUid Leader;
}
