namespace Content.Server.CMU14.Medical.Anatomy.Organs.Brain;

// the forced sleep a brain removal put on the body, so putting a brain back only lifts our own sleep
[RegisterComponent, Access(typeof(BrainSystem))]
public sealed partial class CMUBrainlessSleepComponent : Component
{
    [ViewVariables]
    public EntityUid? Effect;
}
