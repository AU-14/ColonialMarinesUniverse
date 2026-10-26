using Content.Shared.CMU14.Pushups;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Gym;

/// <summary>
///     Someone who punched themselves out on a punching bag can't throw another punch at one until they've rested.
/// </summary>
public sealed partial class CMUPunchingBagSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUPushupsComponent, AttackAttemptEvent>(OnAttackAttempt);
    }

    private void OnAttackAttempt(Entity<CMUPushupsComponent> ent, ref AttackAttemptEvent args)
    {
        if (args.Cancelled ||
            ent.Comp.PunchTiredUntil is not { } until ||
            _timing.CurTime >= until ||
            !HasComp<CMUPunchingBagComponent>(args.Target))
        {
            return;
        }

        args.Cancel();
        _popup.PopupClient(Loc.GetString("cmu-punching-tired"), ent, ent);
    }
}
