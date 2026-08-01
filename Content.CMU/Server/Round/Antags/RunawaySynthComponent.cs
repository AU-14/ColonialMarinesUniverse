namespace Content.Server.AU14.Round.Antags;
[RegisterComponent]
public sealed partial class RunawaySynthComponent : Component
{




    [DataField]
    public TimeSpan TimerWait = TimeSpan.FromSeconds(20);

}
