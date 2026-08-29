namespace Content.Server.CMU14.Round.Antags;
[RegisterComponent]
public sealed partial class FugitiveComponent : Component
{




    [DataField]
    public TimeSpan TimerWait = TimeSpan.FromSeconds(20);

}
