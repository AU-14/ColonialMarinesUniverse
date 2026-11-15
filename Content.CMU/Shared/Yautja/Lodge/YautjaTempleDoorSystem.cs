using Content.Shared.Doors;
using Content.Shared.Examine;
using Content.Shared.Prying.Components;

namespace Content.Shared.CMU14.Yautja.Lodge;

public sealed partial class YautjaTempleDoorSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaTempleDoorComponent, BeforeDoorOpenedEvent>(OnBeforeOpened);
        SubscribeLocalEvent<YautjaTempleDoorComponent, BeforeDoorClosedEvent>(OnBeforeClosed);
        SubscribeLocalEvent<YautjaTempleDoorComponent, BeforePryEvent>(OnBeforePry);
        SubscribeLocalEvent<YautjaTempleDoorComponent, ExaminedEvent>(OnExamined);
    }

    // only the wristblades work these, and that lives on the server. wristblades can pry, so without this the
    // client predicts a pry doafter the server never runs and the bar gets cancelled halfway
    private void OnBeforePry(Entity<YautjaTempleDoorComponent> ent, ref BeforePryEvent args)
    {
        args.Cancelled = true;
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
