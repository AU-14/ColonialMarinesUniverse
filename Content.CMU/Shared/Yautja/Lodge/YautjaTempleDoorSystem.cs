using Content.Shared.Doors;
using Content.Shared.Examine;

namespace Content.Shared.CMU14.Yautja.Lodge;

public sealed partial class YautjaTempleDoorSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaTempleDoorComponent, BeforeDoorOpenedEvent>(OnBeforeOpened);
        SubscribeLocalEvent<YautjaTempleDoorComponent, BeforeDoorClosedEvent>(OnBeforeClosed);
        SubscribeLocalEvent<YautjaTempleDoorComponent, ExaminedEvent>(OnExamined);
    }

    // the lock holds against anything, crowbars and xeno prying included
    private void OnBeforeOpened(Entity<YautjaTempleDoorComponent> ent, ref BeforeDoorOpenedEvent args)
    {
        if (ent.Comp.Locked)
            args.Cancel();
    }

    private void OnBeforeClosed(Entity<YautjaTempleDoorComponent> ent, ref BeforeDoorClosedEvent args)
    {
        if (ent.Comp.Locked)
            args.Cancel();
    }

    private void OnExamined(Entity<YautjaTempleDoorComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Locked)
            args.PushMarkup(Loc.GetString("cmu-yautja-temple-door-examine-locked"));
    }
}
