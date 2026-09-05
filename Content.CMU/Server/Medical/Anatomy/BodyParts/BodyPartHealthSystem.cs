using Content.Shared.CMU14.Medical.Anatomy.BodyParts;

namespace Content.Server.CMU14.Medical.Anatomy.BodyParts;

public sealed class BodyPartHealthSystem : SharedBodyPartHealthSystem
{
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateServer(frameTime);
    }

}
