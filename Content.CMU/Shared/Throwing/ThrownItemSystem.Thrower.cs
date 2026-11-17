namespace Content.Shared.Throwing;

public sealed partial class ThrownItemSystem
{
    public void SetThrower(Entity<ThrownItemComponent> ent, EntityUid? thrower)
    {
        if (ent.Comp.Thrower == thrower)
            return;

        var previous = ent.Comp.Thrower;
        ent.Comp.Thrower = thrower;
        Dirty(ent);
        var ev = new ThrownItemThrowerChangedEvent(previous);
        RaiseLocalEvent(ent, ref ev);
    }
}
