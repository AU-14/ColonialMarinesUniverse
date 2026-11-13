using Content.Shared.Examine;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Yautja.BadBlood;

// the scuttling bracer's lock and countdown text. shared so the client refuses the pickup/unequip itself instead of
// letting it through and snapping the bracer back; starting, beeping and detonating stay on the server
public abstract partial class SharedBadBloodScuttleSystem : EntitySystem
{
    [Dependency] protected IGameTiming Timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodScuttleComponent, GettingPickedUpAttemptEvent>(OnPickupAttempt);
        SubscribeLocalEvent<BadBloodScuttleComponent, BeingUnequippedAttemptEvent>(OnUnequipAttempt);
        SubscribeLocalEvent<BadBloodScuttleComponent, ExaminedEvent>(OnExamined);
    }

    private void OnPickupAttempt(Entity<BadBloodScuttleComponent> ent, ref GettingPickedUpAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnUnequipAttempt(Entity<BadBloodScuttleComponent> ent, ref BeingUnequippedAttemptEvent args)
    {
        args.Cancel();
        args.Reason = "cmu-bad-blood-scuttle-locked";
    }

    private void OnExamined(Entity<BadBloodScuttleComponent> ent, ref ExaminedEvent args)
    {
        var seconds = Math.Max(0, (int) Math.Ceiling((ent.Comp.DetonateAt - Timing.CurTime).TotalSeconds));
        args.PushMarkup(Loc.GetString("cmu-bad-blood-scuttle-examine", ("seconds", seconds)));
    }
}
