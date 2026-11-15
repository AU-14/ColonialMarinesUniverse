namespace Content.Shared.Item;

public sealed partial class HeldSpeedModifierSystem
{
    /// <summary>Gives portable machinery the same weight while held and while pulled.</summary>
    public void CMUSetPortableSpeed(EntityUid uid, float speed)
    {
        var modifier = EnsureComp<HeldSpeedModifierComponent>(uid);
        modifier.WalkModifier = speed;
        modifier.SprintModifier = speed;
        modifier.MirrorClothingModifier = false;
        Dirty(uid, modifier);
    }
}
