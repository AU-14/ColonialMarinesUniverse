using Content.Shared._CMU14.Medical.Treatment.FirstAid;

namespace Content.Server._CMU14.Medical.Treatment.FirstAid;

public sealed class CMUSplintItemSystem : SharedCMUSplintItemSystem
{
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateServer(frameTime);
    }

}
