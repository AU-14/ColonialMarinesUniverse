using Content.Shared.CMU14.Medical.Anatomy.Organs;

namespace Content.Server.CMU14.Medical.Anatomy.Organs;

public sealed class OrganHealthSystem : SharedOrganHealthSystem
{
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateServer(frameTime);
    }

}
