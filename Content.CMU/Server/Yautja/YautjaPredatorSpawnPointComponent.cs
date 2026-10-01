namespace Content.Server.CMU14.Yautja;

[RegisterComponent]
public sealed partial class YautjaPredatorSpawnPointComponent : Component
{
    [DataField(required: true)]
    public YautjaSpawnKind Kind;
}
