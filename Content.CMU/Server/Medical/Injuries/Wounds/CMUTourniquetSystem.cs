using Content.Shared.CMU14.Medical.Injuries.Wounds;

namespace Content.Server.CMU14.Medical.Injuries.Wounds;

public sealed class CMUTourniquetSystem : SharedCMUTourniquetSystem
{
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateServer(frameTime);
    }

}
