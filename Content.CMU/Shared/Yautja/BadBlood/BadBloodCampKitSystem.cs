using Content.Shared._RMC14.Deploy;
using Content.Shared.DoAfter;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Network;

namespace Content.Shared.CMU14.Yautja.BadBlood;

// the Lodge kit rides on the RMC deployable. this runs ahead of it and refuses everyone but the Leader
public sealed partial class BadBloodCampKitSystem : EntitySystem
{
    [Dependency] private BadBloodSystem _badBlood = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodCampKitComponent, UseInHandEvent>(OnUseInHand, before: new[] { typeof(RMCDeploySystem) });
        SubscribeLocalEvent<BadBloodCampKitComponent, DoAfterAttemptEvent<RMCDeployDoAfterEvent>>(OnDeployAttempt);
    }

    private void OnUseInHand(Entity<BadBloodCampKitComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (!_badBlood.IsBadBloodLeader(args.User))
        {
            _popup.PopupClient(Loc.GetString("cmu-bad-blood-camp-kit-leader-only"), args.User, args.User, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        if (!CanDeploy(ent, args.User, false))
            args.Handled = true;
    }

    // the do-after asks every tick, so a Leader who is voted out or dies mid-deploy loses it
    private void OnDeployAttempt(Entity<BadBloodCampKitComponent> ent, ref DoAfterAttemptEvent<RMCDeployDoAfterEvent> args)
    {
        if (args.Cancelled)
            return;

        if (!_badBlood.IsBadBloodLeader(args.Event.User) || !CanDeploy(ent, args.Event.User, true))
            args.Cancel();
    }

    private bool CanDeploy(EntityUid kit, EntityUid user, bool quiet)
    {
        // the clan's state is only on the server
        if (_net.IsClient)
            return true;

        var attempt = new BadBloodCampKitDeployAttemptEvent(user, quiet);
        RaiseLocalEvent(kit, ref attempt);
        return !attempt.Cancelled;
    }
}
