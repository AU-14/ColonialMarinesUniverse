using Content.Shared._CMU14.Medical.Injuries.Wounds;

namespace Content.Server._CMU14.Medical.Injuries.Wounds;

public sealed class CMUTourniquetSystem : SharedCMUTourniquetSystem
{
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateServer(frameTime);
    }

}
