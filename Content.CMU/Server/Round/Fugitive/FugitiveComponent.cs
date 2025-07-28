namespace Content.Server.AU14.Round.Antags;
[RegisterComponent]
public sealed partial class FugitiveComponent : Component
{




    [DataField]
    public TimeSpan TimerWait = TimeSpan.FromSeconds(20);

}
